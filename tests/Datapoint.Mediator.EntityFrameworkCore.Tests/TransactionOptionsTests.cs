using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

// Isolation levels, read-only query transactions, initializers, the transaction context and foreign transactions.
public sealed class TransactionOptionsTests : IAsyncLifetime
{
    private static readonly System.Threading.CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SqlLog _sql = new();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(Ct);
        await using var provider = Build(_ => { });
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync(Ct);
        _sql.Entries.Clear();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task IsolationLevel_IsUsedForCommands()
    {
        await using var provider = Build(t => t.IsolationLevel = IsolationLevel.Serializable);

        await SendAsync(provider, new CreateInvoice(1));

        Assert.Equal(["begin Serializable", "commit"], _sql.Entries);
    }

    [Fact]
    public async Task QueryTransactions_AreReadOnly_RunTheStatementFirst_AndRollBack()
    {
        await using var provider = Build(
            t =>
            {
                t.Queries = QueryTransactions.ReadOnly;
                t.QueryIsolationLevel = IsolationLevel.Serializable;
                t.ReadOnlyStatement = "SELECT 'read only'";
            },
            withInitializer: true);

        var seen = await QueryAsync(provider, new ReadContext());

        Assert.Equal(["begin Serializable", "SELECT 'read only'", "SELECT 'initialize'", "SELECT 'handler'", "rollback"], _sql.Entries);
        Assert.Equal("active: True readonly: True joined: False attempt: 1", seen);
        var initialization = Assert.Single(Recorder(provider).Initializations);
        Assert.True(initialization.IsReadOnly);
        Assert.IsType<ReadContext>(initialization.Request);
    }

    [Fact]
    public async Task QueryTransactions_DiscardWhatAQueryChanged()
    {
        await using var provider = Build(t => t.Queries = QueryTransactions.ReadOnly);
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.QueryAsync(new WriteFromQuery(), Ct);
        await mediator.SendAsync(new CreateInvoice(1), Ct);   // must not save what the query added

        Assert.Equal(new[] { 1 }, await AmountsAsync(provider));
    }

    [Fact]
    public async Task NoQueryTransactions_QueriesRunWithoutTransaction()
    {
        await using var provider = Build(_ => { });

        Assert.Equal("active: False readonly: False joined: False attempt: 0", await QueryAsync(provider, new ReadContext()));
        Assert.DoesNotContain(_sql.Entries, e => e.StartsWith("begin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonTransactionalQuery_RunsWithoutTransaction()
    {
        await using var provider = Build(t => t.Queries = QueryTransactions.ReadOnly);

        await QueryAsync(provider, new CountWithoutTransaction());

        Assert.Equal(["active: False readonly: False joined: False attempt: 0"], Recorder(provider).Log);
        Assert.Empty(_sql.Entries);
    }

    [Fact]
    public async Task NestedQuery_JoinsTheCommand_AndTheOuterStateComesBack()
    {
        await using var provider = Build(t => t.Queries = QueryTransactions.ReadOnly, withInitializer: true);

        await SendAsync(provider, new SendNestedAndRecord());

        Assert.Equal(
            [
                "initialize SendNestedAndRecord joined: False context active: True",
                "outer before active: True readonly: False joined: False attempt: 1",
                "initialize ReadContext joined: True context active: True",
                "nested active: True readonly: False joined: True attempt: 1",
                "outer after active: True readonly: False joined: False attempt: 1",
            ],
            Recorder(provider).Log);
        Assert.Equal(Recorder(provider).Initializations[0].AttemptId, Recorder(provider).Initializations[1].AttemptId);
        Assert.Equal(["begin Unspecified", "SELECT 'initialize'", "SELECT 'handler'", "commit"], _sql.Entries);
    }

    [Fact]
    public async Task ParallelDispatches_SeeTheirOwnState()
    {
        // Each dispatch needs its own connection, so each gets its own database.
        await using var other = new SqliteConnection("DataSource=:memory:");
        await other.OpenAsync(Ct);
        var gate = new Gate(2);
        await using var first = Build(_ => { }, gate: gate);
        await using var second = Build(_ => { }, connection: other, gate: gate);
        await using (var scope = second.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync(Ct);
        }

        var ids = await Task.WhenAll(
            Task.Run(() => SendAsync(first, new WaitForOther()), Ct),
            Task.Run(() => SendAsync(second, new WaitForOther()), Ct));

        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    public async Task Initializers_RunOnEveryAttempt_WithANewAttemptId()
    {
        await using var provider = Build(_ => { }, withInitializer: true, retrying: true);

        await SendAsync(provider, new CreateInvoiceFailingOnce(100));

        Assert.Equal([1, 2], Recorder(provider).Initializations.Select(i => i.Attempt));
        Assert.NotEqual(Recorder(provider).Initializations[0].AttemptId, Recorder(provider).Initializations[1].AttemptId);
        Assert.Equal(new[] { 100 }, await AmountsAsync(provider));
    }

    [Fact]
    public async Task InitializerFails_TheCommandFailsAndNothingIsSaved()
    {
        await using var provider = Build(_ => { }, withInitializer: true);
        Recorder(provider).FailInitializer = true;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await SendAsync(provider, new CreateInvoice(1)));

        Assert.Empty(await AmountsAsync(provider));
        Assert.Equal(["begin Unspecified", "SELECT 'initialize'"], _sql.Entries);
    }

    [Fact]
    public async Task ForeignTransaction_Joined_IsSavedOnlyByItsOwner()
    {
        await using var provider = Build(_ => { }, withInitializer: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(new CreateInvoice(1), Ct);

        // The behaviour didn't save, prepare or commit the application's transaction.
        Assert.Empty(Recorder(provider).Initializations);
        Assert.Single(db.ChangeTracker.Entries<Invoice>(), e => e.State == EntityState.Added);
        await db.SaveChangesAsync(Ct);
        await transaction.CommitAsync(Ct);
        Assert.Equal(new[] { 1 }, await AmountsAsync(provider));
    }

    [Fact]
    public async Task ForeignTransaction_Refused_Throws()
    {
        await using var provider = Build(t =>
        {
            t.ForeignTransactions = ForeignTransactions.Throw;
            t.Queries = QueryTransactions.ReadOnly;
        });
        await using var scope = provider.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(Ct);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.SendAsync(new CreateInvoice(1), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.QueryAsync(new ReadContext(), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.StreamAsync(new StreamAmounts(), Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task ForeignTransaction_OnAnotherContextInstance_IsNotMistakenForTheMediators()
    {
        await using var outerDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await using var otherConnection = new SqliteConnection("DataSource=:memory:");
        await using var innerDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(otherConnection).Options);
        var inner = new TransactionBehavior<AppDbContext>(innerDb, [], new TransactionBehaviorOptions { ForeignTransactions = ForeignTransactions.Throw });
        Exception? refused = null;

        // While the mediator's transaction is active on one context, another context begins its own and sends a command.
        await new TransactionBehavior<AppDbContext>(outerDb).HandleAsync(
            new CreateInvoice(1),
            async ct =>
            {
                await using var transaction = await innerDb.Database.BeginTransactionAsync(ct);
                refused = await Record.ExceptionAsync(async () => await inner.HandleAsync(new CreateInvoice(2), _ => new ValueTask<Guid>(Guid.Empty), ct));
                return Guid.Empty;
            },
            Ct);

        Assert.IsType<InvalidOperationException>(refused);
        Assert.Contains("didn't begin", refused.Message);
    }

    [Fact]
    public void TransactionContext_WithoutTransaction_IsInactive()
    {
        var context = new TransactionContext();

        Assert.Equal("active: False readonly: False joined: False attempt: 0", ContextText.Of(context));
        Assert.Equal(Guid.Empty, context.AttemptId);
    }

    [Fact]
    public void Options_RejectUndefinedValues()
    {
        var options = new TransactionBehaviorOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.IsolationLevel = (IsolationLevel)12345);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.QueryIsolationLevel = (IsolationLevel)12345);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Queries = (QueryTransactions)7);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ForeignTransactions = (ForeignTransactions)7);
        Assert.Throws<ArgumentException>(() => options.ReadOnlyStatement = " ");
        options.ReadOnlyStatement = null;
        Assert.Null(options.ReadOnlyStatement);
    }

    [Fact]
    public async Task Options_ChangedAfterTheBehaviorIsCreated_HaveNoEffect()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).AddInterceptors(_sql).Options);
        var options = new TransactionBehaviorOptions();
        var behavior = new TransactionBehavior<AppDbContext>(db, [], options);
        options.IsolationLevel = IsolationLevel.Serializable;

        await behavior.HandleAsync(new CreateInvoice(1), _ => new ValueTask<Guid>(Guid.Empty), Ct);

        Assert.Equal(["begin Unspecified", "commit"], _sql.Entries);
    }

    [Fact]
    public void Constructor_NullInitializers_Throws()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

        Assert.Equal("initializers", Assert.Throws<ArgumentNullException>(() => new TransactionBehavior<AppDbContext>(db, null!)).ParamName);
    }

    private static Recorder Recorder(ServiceProvider provider) => provider.GetRequiredService<Recorder>();

    private ServiceProvider Build(
        Action<TransactionBehaviorOptions> configure,
        bool withInitializer = false,
        bool retrying = false,
        SqliteConnection? connection = null,
        Gate? gate = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddSingleton(gate ?? new Gate(1))
            .AddTransactionContext()
            .AddDbContext<AppDbContext>(o => o
                .UseSqlite(connection ?? _connection, s =>
                {
                    if (retrying)
                    {
                        s.ExecutionStrategy(d => new RetryingTestStrategy(d));
                    }
                })
                .AddInterceptors(_sql, _sql.Commands))
            .AddMediator(o => o.AddHandlersFromAssemblyContaining<TransactionOptionsTests>().AddTransactionBehavior<AppDbContext>(configure));

        if (withInitializer)
        {
            services.AddTransactionInitializer<AppDbContext, RecordingInitializer>();
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<TResult> SendAsync<TResult>(ServiceProvider provider, ICommand<TResult> command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, Ct);
    }

    private static async Task SendAsync(ServiceProvider provider, ICommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(command, Ct);
    }

    private static async Task<TResult> QueryAsync<TResult>(ServiceProvider provider, IQuery<TResult> query)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().QueryAsync(query, Ct);
    }

    private static async Task<int[]> AmountsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.OrderBy(i => i.Amount).Select(i => i.Amount).ToArrayAsync(Ct);
    }
}
