using System;
using System.Linq;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

public sealed class TransactionBehaviorTests : IAsyncLifetime
{
    // An in-memory SQLite database lives as long as its connection, so one connection is shared by every scope.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        _provider = BuildProvider(o => o.UseSqlite(_connection));
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public void Constructor_NullDbContext_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new TransactionBehavior<AppDbContext>(null!));

        Assert.Equal("dbContext", exception.ParamName);
    }

    [Fact]
    public async Task HandleAsync_NullArguments_Throw()
    {
        await using var scope = _provider.CreateAsyncScope();
        var behavior = new TransactionBehavior<AppDbContext>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        BehaviorArguments.AssertNullsAreRefused(behavior, new CreateTwoInvoices(), new CreateInvoice(1), new CountInvoices(), new StreamAmounts());
    }

    [Fact]
    public async Task Command_Success_IsSavedAndCommittedInATransaction()
    {
        var id = await SendAsync(new CreateInvoice(100));

        Assert.Equal(100, (await FindAsync(id))?.Amount);
        Assert.Matches("^create in transaction [0-9a-f-]{36}$", Assert.Single(Recorder.Log));
    }

    [Fact]
    public async Task Command_Failure_RollsBackAndSavesNothing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await SendAsync(new CreateInvoiceThenFail(100)));

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task NonTransactionalCommand_RunsWithoutTransaction()
    {
        await SendAsync(new CreateInvoiceWithoutTransaction(100));

        Assert.Equal(["transaction is null: True"], Recorder.Log);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task NestedCommand_JoinsOuterTransaction()
    {
        await SendAsync(new CreateTwoInvoices());

        var outer = Recorder.Log[0]["outer in transaction ".Length..];
        var inner = Recorder.Log[1]["create in transaction ".Length..];
        Assert.Equal(outer, inner);
        Assert.Equal(2, await CountAsync());
    }

    [Fact]
    public async Task QueriesAndMessages_RunWithoutTransaction()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.QueryAsync(new CountInvoices(), TestContext.Current.CancellationToken);
        await mediator.PublishAsync(new InvoicesChanged(), TestContext.Current.CancellationToken);

        Assert.Equal(["query transaction is null: True", "message transaction is null: True"], Recorder.Log);
    }

    [Fact]
    public async Task RetryingStrategy_RunsCommandAgainAndSavesOnce()
    {
        await using var provider = BuildProvider(o => o.UseSqlite(_connection, s => s.ExecutionStrategy(d => new RetryingTestStrategy(d))));
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(new CreateInvoiceFailingOnce(100), TestContext.Current.CancellationToken);

        Assert.Equal(2, provider.GetRequiredService<Recorder>().Attempts);
        Assert.Equal(1, await CountAsync());
    }

    private Recorder Recorder => _provider.GetRequiredService<Recorder>();

    private static ServiceProvider BuildProvider(Action<DbContextOptionsBuilder> configureDb) =>
        new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddSingleton(new Gate(1))
            .AddTransactionContext()
            .AddDbContext<AppDbContext>(configureDb)
            .AddMediator(o => o.AddHandlersFromAssemblyContaining<TransactionBehaviorTests>().AddTransactionBehavior<AppDbContext>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private async Task<TResult> SendAsync<TResult>(ICommand<TResult> command)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, TestContext.Current.CancellationToken);
    }

    private async Task SendAsync(ICommand command)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, TestContext.Current.CancellationToken);
    }

    // Reads through a new scope, so the result reflects what was committed, not what a context still tracks.
    private async Task<Invoice?> FindAsync(Guid id)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.FindAsync([id], TestContext.Current.CancellationToken);
    }

    private async Task<int> CountAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.CountAsync(TestContext.Current.CancellationToken);
    }
}
