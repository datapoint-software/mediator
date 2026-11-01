using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

// The pipeline recommended for authorizing inside the transaction, with the real authorization behaviours.
public sealed class AuthorizationInTransactionTests : IAsyncLifetime
{
    private static readonly System.Threading.CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SqlLog _sql = new();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(Ct);
        await using var provider = Build(SignedIn());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync(Ct);
        _sql.Entries.Clear();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Command_IsAuthorizedInsideItsPreparedTransaction()
    {
        await using var provider = Build(SignedIn());

        await SendAsync(provider, new CreateInvoice(1));

        Assert.Equal(
            ["initialize CreateInvoice joined: False context active: True",
             "authorize CreateInvoice active: True readonly: False joined: False attempt: 1",
             "observed Allowed",
             "create in transaction " + Recorder(provider).Log[3]["create in transaction ".Length..]],
            Recorder(provider).Log);
        Assert.Equal(["begin Unspecified", "SELECT 'initialize'", "commit"], _sql.Entries);
    }

    [Fact]
    public async Task Query_IsAuthorizedInsideItsReadOnlyTransaction()
    {
        await using var provider = Build(SignedIn());

        await QueryAsync(provider, new ReadContext());

        Assert.Contains("authorize ReadContext active: True readonly: True joined: False attempt: 1", Recorder(provider).Log);
    }

    [Fact]
    public async Task StreamQuery_IsAuthorizedInsideItsTransaction_AtFirstEnumeration()
    {
        await using var provider = Build(SignedIn());
        await using var scope = provider.CreateAsyncScope();
        var results = scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmounts(), Ct);

        Assert.Empty(Recorder(provider).Log);
        await results.ToListAsync(Ct);

        Assert.Contains("authorize StreamAmounts active: True readonly: True joined: False attempt: 1", Recorder(provider).Log);
    }

    [Fact]
    public async Task NotSignedIn_IsRefusedBeforeTheTransactionBegins_AndObservedOnce()
    {
        await using var provider = Build(principal: null);

        await Assert.ThrowsAsync<AuthenticationRequiredException>(async () => await SendAsync(provider, new CreateInvoice(1)));

        Assert.Equal(["observed AuthenticationRequired"], Recorder(provider).Log);
        Assert.Empty(_sql.Entries);
    }

    [Fact]
    public async Task Denial_RollsTheTransactionBack()
    {
        await using var provider = Build(SignedIn());
        Recorder(provider).Deny = true;

        await Assert.ThrowsAsync<AccessDeniedException>(async () => await SendAsync(provider, new CreateInvoice(1)));

        Assert.Equal(["begin Unspecified", "SELECT 'initialize'"], _sql.Entries);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.CountAsync(Ct));
    }

    [Fact]
    public async Task Retry_AuthorizesAgain()
    {
        await using var provider = Build(SignedIn(), retrying: true);

        await SendAsync(provider, new CreateInvoiceFailingOnce(1));

        Assert.Equal(
            ["authorize CreateInvoiceFailingOnce active: True readonly: False joined: False attempt: 1",
             "authorize CreateInvoiceFailingOnce active: True readonly: False joined: False attempt: 2"],
            Recorder(provider).Log.Where(e => e.StartsWith("authorize", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AuthorizationRegisteredBeforeTheTransaction_FailsInsteadOfAuthorizingOutside()
    {
        await using var provider = Build(SignedIn(), authorizationFirst: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await SendAsync(provider, new CreateInvoice(1)));

        Assert.Contains("inside a transaction", exception.Message);
        Assert.DoesNotContain(Recorder(provider).Log, e => e.StartsWith("authorize", StringComparison.Ordinal));
    }

    [Fact]
    public void StartupCheck_RefusesAuthorizedNonTransactionalActions()
    {
        var command = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddHandler<CreateInvoiceWithoutTransactionHandler>()
            .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.Commands)));
        var query = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddHandler<CountWithoutTransactionHandler>()
            .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.CommandsAndQueries)));
        var stream = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddHandler<StreamAmountsWithoutTransactionHandler>()
            .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.CommandsAndQueries)));

        Assert.Contains(nameof(CreateInvoiceWithoutTransaction), command.Message);
        Assert.Contains(nameof(CountWithoutTransaction), query.Message);
        Assert.Contains(nameof(StreamAmountsWithoutTransaction), stream.Message);
    }

    [Fact]
    public void StartupCheck_AllowsWhatDoesNotNeedATransaction()
    {
        // A non-transactional query when only commands are required, an anonymous one, and no requirement at all.
        new ServiceCollection().AddMediator(o => o
            .AddHandler<CountWithoutTransactionHandler>()
            .AddHandler<AnonymousWithoutTransactionHandler>()
            .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.Commands));
        new ServiceCollection().AddMediator(o => o
            .AddHandler<CreateInvoiceWithoutTransactionHandler>()
            .AddAuthorizationBehavior(_ => { }));
    }

    private static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    private static Recorder Recorder(ServiceProvider provider) => provider.GetRequiredService<Recorder>();

    private ServiceProvider Build(ClaimsPrincipal? principal, bool retrying = false, bool authorizationFirst = false) =>
        new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddSingleton(new Gate(1))
            .AddTransactionContext()
            .AddTransactionInitializer<AppDbContext, RecordingInitializer>()
            .AddFixedPrincipalAccessor(principal)
            .AddAuthorizationObserver<RecordingObserver>()
            .AddScoped<IAuthorizer<CreateInvoice>, RecordingAuthorizer>()
            .AddScoped<IAuthorizer<CreateInvoiceFailingOnce>, RecordingAuthorizer>()
            .AddScoped<IAuthorizer<ReadContext>, RecordingAuthorizer>()
            .AddScoped<IAuthorizer<StreamAmounts>, RecordingAuthorizer>()
            .AddDbContext<AppDbContext>(o => o
                .UseSqlite(_connection, s =>
                {
                    if (retrying)
                    {
                        s.ExecutionStrategy(d => new RetryingTestStrategy(d));
                    }
                })
                .AddInterceptors(_sql, _sql.Commands))
            .AddMediator(o =>
            {
                o.AddHandler<CreateInvoiceHandler>()
                    .AddHandler<CreateInvoiceFailingOnceHandler>()
                    .AddHandler<ReadContextHandler>()
                    .AddHandler<StreamAmountsHandler>()
                    .AddAuthenticationBehavior();

                void Transaction() => o.AddTransactionBehavior<AppDbContext>(t => t.Queries = QueryTransactions.ReadOnly);
                void Authorization() => o.AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.CommandsAndQueries);

                if (authorizationFirst)
                {
                    Authorization();
                    Transaction();
                }
                else
                {
                    Transaction();
                    Authorization();
                }
            })
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static async Task SendAsync(ServiceProvider provider, ICommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, Ct);
    }

    private static async Task SendAsync<TResult>(ServiceProvider provider, ICommand<TResult> command)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, Ct);
    }

    private static async Task QueryAsync<TResult>(ServiceProvider provider, IQuery<TResult> query)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().QueryAsync(query, Ct);
    }
}
