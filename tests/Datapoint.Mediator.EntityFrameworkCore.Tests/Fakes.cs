using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
