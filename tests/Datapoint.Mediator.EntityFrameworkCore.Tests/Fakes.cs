using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

public sealed class Invoice
{
    public Guid Id { get; set; }

    public int Amount { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
}

// Shared across scopes, to observe what handlers saw.
public sealed class Recorder
{
    public List<string> Log { get; } = [];

    public int Attempts { get; set; }

    public List<TransactionInitialization> Initializations { get; } = [];

    public bool FailInitializer { get; set; }

    public bool Deny { get; set; }
}

public sealed class TransientTestException : Exception;

// Retries TransientTestException, the way SQL Server's strategy retries transient errors.
public sealed class RetryingTestStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(1))
{
    protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;
}

public sealed record CreateInvoice(int Amount) : ICommand<Guid>;

public sealed record CreateInvoiceThenFail(int Amount) : ICommand;

[NonTransactional]
public sealed record CreateInvoiceWithoutTransaction(int Amount) : ICommand;

public sealed record CreateTwoInvoices : ICommand;

public sealed record CreateInvoiceFailingOnce(int Amount) : ICommand;

public sealed record CountInvoices : IQuery<int>;

public sealed record InvoicesChanged : IMessage;

public sealed class CreateInvoiceHandler(AppDbContext db, Recorder recorder) : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"create in transaction {db.Database.CurrentTransaction?.TransactionId}");
        var invoice = new Invoice { Id = Guid.NewGuid(), Amount = command.Amount };
        db.Invoices.Add(invoice);
        return new(invoice.Id);
    }
}

public sealed class CreateInvoiceThenFailHandler(AppDbContext db) : ICommandHandler<CreateInvoiceThenFail>
{
    public ValueTask HandleAsync(CreateInvoiceThenFail command, CancellationToken ct = default)
    {
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = command.Amount });
        throw new InvalidOperationException("handler failed");
    }
}

public sealed class CreateInvoiceWithoutTransactionHandler(AppDbContext db, Recorder recorder)
    : ICommandHandler<CreateInvoiceWithoutTransaction>
{
    public async ValueTask HandleAsync(CreateInvoiceWithoutTransaction command, CancellationToken ct = default)
    {
        recorder.Log.Add($"transaction is null: {db.Database.CurrentTransaction is null}");
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = command.Amount });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CreateTwoInvoicesHandler(AppDbContext db, IMediator mediator, Recorder recorder) : ICommandHandler<CreateTwoInvoices>
{
    public async ValueTask HandleAsync(CreateTwoInvoices command, CancellationToken ct = default)
    {
        recorder.Log.Add($"outer in transaction {db.Database.CurrentTransaction?.TransactionId}");
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = 1 });
        await mediator.SendAsync(new CreateInvoice(2), ct);
    }
}

public sealed class CreateInvoiceFailingOnceHandler(AppDbContext db, Recorder recorder) : ICommandHandler<CreateInvoiceFailingOnce>
{
    public ValueTask HandleAsync(CreateInvoiceFailingOnce command, CancellationToken ct = default)
    {
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = command.Amount });

        if (++recorder.Attempts == 1)
        {
            throw new TransientTestException();
        }

        return default;
    }
}

public sealed class CountInvoicesHandler(AppDbContext db, Recorder recorder) : IQueryHandler<CountInvoices, int>
{
    public async ValueTask<int> HandleAsync(CountInvoices query, CancellationToken ct = default)
    {
        recorder.Log.Add($"query transaction is null: {db.Database.CurrentTransaction is null}");
        return await db.Invoices.CountAsync(ct);
    }
}

public sealed class InvoicesChangedHandler(AppDbContext db, Recorder recorder) : IMessageHandler<InvoicesChanged>
{
    public ValueTask HandleAsync(InvoicesChanged message, CancellationToken ct = default)
    {
        recorder.Log.Add($"message transaction is null: {db.Database.CurrentTransaction is null}");
        return default;
    }
}

// Records transactions and commands in order, as "begin Serializable", the command text, "commit" or "rollback".
public sealed class SqlLog : DbTransactionInterceptor
{
    public List<string> Entries { get; } = [];

    public override ValueTask<InterceptionResult<System.Data.Common.DbTransaction>> TransactionStartingAsync(
        System.Data.Common.DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<System.Data.Common.DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        Entries.Add($"begin {eventData.IsolationLevel}");
        return base.TransactionStartingAsync(connection, eventData, result, cancellationToken);
    }

    public override Task TransactionCommittedAsync(
        System.Data.Common.DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Entries.Add("commit");
        return Task.CompletedTask;
    }

    public override Task TransactionRolledBackAsync(
        System.Data.Common.DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Entries.Add("rollback");
        return Task.CompletedTask;
    }

    public CommandLog Commands => new(this);

    public sealed class CommandLog(SqlLog log) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        // Only marker statements, so the log isn't tied to the SQL EF Core generates.
        private void Record(System.Data.Common.DbCommand command)
        {
            if (command.CommandText.StartsWith("SELECT '", StringComparison.Ordinal))
            {
                log.Entries.Add(command.CommandText);
            }
        }
    }
}

// Records what it's given, runs a marker statement in new transactions, and fails when told to.
public sealed class RecordingInitializer(Recorder recorder, ITransactionContext context) : ITransactionInitializer<AppDbContext>
{
    public async ValueTask InitializeAsync(AppDbContext dbContext, TransactionInitialization transaction, CancellationToken ct = default)
    {
        recorder.Initializations.Add(transaction);
        recorder.Log.Add($"initialize {transaction.Request.GetType().Name} joined: {transaction.IsJoined} context active: {context.IsActive}");

        if (!transaction.IsJoined)
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 'initialize'", ct);
        }

        if (recorder.FailInitializer)
        {
            throw new InvalidOperationException("initializer failed");
        }
    }
}

public sealed record ReadContext : IQuery<string>;

public sealed record WriteFromQuery : IQuery<int>;

[NonTransactional]
public sealed record CountWithoutTransaction : IQuery<int>;

public sealed record StreamAmounts : IStreamQuery<int>;

[NonTransactional]
public sealed record StreamAmountsWithoutTransaction : IStreamQuery<int>;

public sealed record StreamThenFail : IStreamQuery<int>;

public sealed record SendNestedAndRecord : ICommand;

public sealed record StreamInsideCommand : ICommand;

public sealed record WaitForOther : ICommand<Guid>;

public static class ContextText
{
    public static string Of(ITransactionContext c) =>
        $"active: {c.IsActive} readonly: {c.IsReadOnly} joined: {c.IsJoined} attempt: {c.Attempt}";
}

public sealed class ReadContextHandler(ITransactionContext context, AppDbContext db) : IQueryHandler<ReadContext, string>
{
    public async ValueTask<string> HandleAsync(ReadContext query, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 'handler'", ct);
        return ContextText.Of(context);
    }
}

public sealed class WriteFromQueryHandler(AppDbContext db) : IQueryHandler<WriteFromQuery, int>
{
    public ValueTask<int> HandleAsync(WriteFromQuery query, CancellationToken ct = default)
    {
        // A query must not change anything: this is never saved.
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = 999 });
        return new(1);
    }
}

public sealed class CountWithoutTransactionHandler(ITransactionContext context, Recorder recorder) : IQueryHandler<CountWithoutTransaction, int>
{
    public ValueTask<int> HandleAsync(CountWithoutTransaction query, CancellationToken ct = default)
    {
        recorder.Log.Add(ContextText.Of(context));
        return new(0);
    }
}

public sealed class StreamAmountsHandler(AppDbContext db, ITransactionContext context, Recorder recorder) : IStreamQueryHandler<StreamAmounts, int>
{
    public async IAsyncEnumerable<int> HandleAsync(StreamAmounts query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var amount in db.Invoices.OrderBy(i => i.Amount).Select(i => i.Amount).AsAsyncEnumerable().WithCancellation(ct))
        {
            await Task.Yield();
            recorder.Log.Add($"stream {ContextText.Of(context)}");
            yield return amount;
        }
    }
}

public sealed class StreamAmountsWithoutTransactionHandler(ITransactionContext context, Recorder recorder)
    : IStreamQueryHandler<StreamAmountsWithoutTransaction, int>
{
    public async IAsyncEnumerable<int> HandleAsync(StreamAmountsWithoutTransaction query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        recorder.Log.Add($"stream {ContextText.Of(context)}");
        yield return 1;
    }
}

public sealed class StreamThenFailHandler(AppDbContext db) : IStreamQueryHandler<StreamThenFail, int>
{
    public async IAsyncEnumerable<int> HandleAsync(StreamThenFail query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), Amount = 999 });
        yield return 1;
        throw new InvalidOperationException("stream failed");
    }
}

public sealed class SendNestedAndRecordHandler(IMediator mediator, ITransactionContext context, Recorder recorder) : ICommandHandler<SendNestedAndRecord>
{
    public async ValueTask HandleAsync(SendNestedAndRecord command, CancellationToken ct = default)
    {
        recorder.Log.Add($"outer before {ContextText.Of(context)}");
        recorder.Log.Add($"nested {await mediator.QueryAsync(new ReadContext(), ct)}");
        recorder.Log.Add($"outer after {ContextText.Of(context)}");
    }
}

public sealed class StreamInsideCommandHandler(IMediator mediator, Recorder recorder) : ICommandHandler<StreamInsideCommand>
{
    public async ValueTask HandleAsync(StreamInsideCommand command, CancellationToken ct = default)
    {
        await foreach (var _ in mediator.StreamAsync(new StreamAmounts(), ct))
        {
        }

        recorder.Log.Add("command done");
    }
}

// Waits until another dispatch is also inside its transaction, then returns its own attempt id.
public sealed class WaitForOtherHandler(ITransactionContext context, Gate gate) : ICommandHandler<WaitForOther, Guid>
{
    public async ValueTask<Guid> HandleAsync(WaitForOther command, CancellationToken ct = default)
    {
        var mine = context.AttemptId;
        await gate.ArriveAndWaitAsync(ct);
        return mine == context.AttemptId ? mine : Guid.Empty;
    }
}

public sealed class Gate(int parties)
{
    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public Task ArriveAndWaitAsync(CancellationToken ct)
    {
        if (Interlocked.Increment(ref _arrived) == parties)
        {
            _allArrived.SetResult();
        }

        return _allArrived.Task.WaitAsync(ct);
    }
}

// Records where it ran, and denies when told to.
public sealed class RecordingAuthorizer(Recorder recorder, ITransactionContext context) :
    Datapoint.Mediator.Authorization.IAuthorizer<CreateInvoice>,
    Datapoint.Mediator.Authorization.IAuthorizer<CreateInvoiceFailingOnce>,
    Datapoint.Mediator.Authorization.IAuthorizer<ReadContext>,
    Datapoint.Mediator.Authorization.IAuthorizer<StreamAmounts>
{
    public ValueTask<Datapoint.Mediator.Authorization.AuthorizationDecision> AuthorizeAsync(CreateInvoice action, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct = default) => Decide(action);

    public ValueTask<Datapoint.Mediator.Authorization.AuthorizationDecision> AuthorizeAsync(CreateInvoiceFailingOnce action, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct = default) => Decide(action);

    public ValueTask<Datapoint.Mediator.Authorization.AuthorizationDecision> AuthorizeAsync(ReadContext action, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct = default) => Decide(action);

    public ValueTask<Datapoint.Mediator.Authorization.AuthorizationDecision> AuthorizeAsync(StreamAmounts action, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct = default) => Decide(action);

    private ValueTask<Datapoint.Mediator.Authorization.AuthorizationDecision> Decide(object action)
    {
        recorder.Log.Add($"authorize {action.GetType().Name} {ContextText.Of(context)}");
        return new(Datapoint.Mediator.Authorization.AuthorizationDecision.AllowIf(!recorder.Deny));
    }
}

public sealed class RecordingObserver(Recorder recorder) : Datapoint.Mediator.Authorization.IAuthorizationObserver
{
    public ValueTask OnAuthorizedAsync(Datapoint.Mediator.Authorization.AuthorizationEvent authorization, CancellationToken ct = default)
    {
        recorder.Log.Add($"observed {authorization.Outcome}");
        return default;
    }
}

[Datapoint.Mediator.Authorization.AllowAnonymousAccess]
[NonTransactional]
public sealed record AnonymousWithoutTransaction : ICommand;

public sealed class AnonymousWithoutTransactionHandler : ICommandHandler<AnonymousWithoutTransaction>
{
    public ValueTask HandleAsync(AnonymousWithoutTransaction command, CancellationToken ct = default) => default;
}
