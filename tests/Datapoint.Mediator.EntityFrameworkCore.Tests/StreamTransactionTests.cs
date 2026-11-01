using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

public sealed class StreamTransactionTests : IAsyncLifetime
{
    private static readonly System.Threading.CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SqlLog _sql = new();
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(Ct);
        _provider = Build(retrying: false);
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(Ct);
        db.Invoices.AddRange(new Invoice { Id = Guid.NewGuid(), Amount = 1 }, new Invoice { Id = Guid.NewGuid(), Amount = 2 });
        await db.SaveChangesAsync(Ct);
        _sql.Entries.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private Recorder Recorder => _provider.GetRequiredService<Recorder>();

    [Fact]
    public async Task Stream_RunsInAReadOnlyTransaction_UntilTheEnumerationCompletes()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ITransactionContext>();
        var consumerSaw = new System.Collections.Generic.List<bool>();

        var amounts = new System.Collections.Generic.List<int>();
        await foreach (var amount in scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmounts(), Ct))
        {
            Assert.DoesNotContain("rollback", _sql.Entries);
            consumerSaw.Add(context.IsActive);
            amounts.Add(amount);
        }

        Assert.Equal([1, 2], amounts);
        Assert.Equal([false, false], consumerSaw);
        Assert.Equal(
            ["initialize StreamAmounts joined: False context active: True",
             "stream active: True readonly: True joined: False attempt: 1",
             "stream active: True readonly: True joined: False attempt: 1"],
            Recorder.Log);
        // Disposing the uncommitted transaction rolls it back.
        Assert.Equal(["begin Unspecified", "SELECT 'read only'", "SELECT 'initialize'"], _sql.Entries);
        Assert.Null(scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.CurrentTransaction);
    }

    [Fact]
    public async Task Stream_StoppedEarly_IsRolledBack()
    {
        await using var scope = _provider.CreateAsyncScope();

        await foreach (var _ in scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmounts(), Ct))
        {
            break;
        }

        Assert.Null(scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.CurrentTransaction);
    }

    [Fact]
    public async Task Stream_Failing_IsRolledBack_AndDiscardsItsChanges()
    {
        await using var scope = _provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.StreamAsync(new StreamThenFail(), Ct).ToListAsync(Ct));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(db.Database.CurrentTransaction);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(2, await db.Invoices.CountAsync(Ct));
    }

    [Fact]
    public async Task Stream_WithARetryingStrategy_IsRefused()
    {
        await using var provider = Build(retrying: true);
        await using var scope = provider.CreateAsyncScope();
        var results = scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmounts(), Ct);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await results.ToListAsync(Ct));

        Assert.Contains("retries on failure", exception.Message);
        Assert.Empty(_sql.Entries);
    }

    [Fact]
    public async Task NonTransactionalStream_RunsWithoutTransaction()
    {
        await using var scope = _provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmountsWithoutTransaction(), Ct).ToListAsync(Ct);

        Assert.Equal(["stream active: False readonly: False joined: False attempt: 0"], Recorder.Log);
        Assert.Empty(_sql.Entries);
    }

    [Fact]
    public async Task StreamInsideACommand_JoinsItsTransaction()
    {
        await using var scope = _provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(new StreamInsideCommand(), Ct);

        Assert.Equal(
            ["initialize StreamInsideCommand joined: False context active: True",
             "initialize StreamAmounts joined: True context active: True",
             "stream active: True readonly: False joined: True attempt: 1",
             "stream active: True readonly: False joined: True attempt: 1",
             "command done"],
            Recorder.Log);
        Assert.Equal(["begin Unspecified", "SELECT 'initialize'", "commit"], _sql.Entries);
    }

    [Fact]
    public async Task StreamInAForeignTransaction_IsJoinedAsItIs()
    {
        await using var scope = _provider.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(Ct);

        var amounts = await scope.ServiceProvider.GetRequiredService<IMediator>().StreamAsync(new StreamAmounts(), Ct).ToListAsync(Ct);

        Assert.Equal([1, 2], amounts);
        Assert.All(Recorder.Log, e => Assert.Equal("stream active: False readonly: False joined: False attempt: 0", e));
    }

    private ServiceProvider Build(bool retrying) =>
        new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddSingleton(new Gate(1))
            .AddTransactionContext()
            .AddTransactionInitializer<AppDbContext, RecordingInitializer>()
            .AddDbContext<AppDbContext>(o => o
                .UseSqlite(_connection, s =>
                {
                    if (retrying)
                    {
                        s.ExecutionStrategy(d => new RetryingTestStrategy(d));
                    }
                })
                .AddInterceptors(_sql, _sql.Commands))
            .AddMediator(o => o
                .AddHandlersFromAssemblyContaining<StreamTransactionTests>()
                .AddTransactionBehavior<AppDbContext>(t =>
                {
                    t.Queries = QueryTransactions.ReadOnly;
                    t.ReadOnlyStatement = "SELECT 'read only'";
                }))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}
