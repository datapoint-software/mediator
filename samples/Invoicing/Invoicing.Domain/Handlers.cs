using System.Runtime.CompilerServices;
using Datapoint.Mediator;
using Datapoint.Mediator.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Invoicing.Domain;

public sealed class CreateInvoiceHandler(InvoicingDbContext db, IPrincipalAccessor principal) : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default)
    {
        var invoice = new Invoice
        {
            // Generated here, because database-generated keys are only known after the save.
            Id = Guid.CreateVersion7(),
            Customer = command.Customer,
            Amount = command.Amount,
            Status = InvoiceStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = principal.Principal?.Identity?.Name ?? "",
        };

        // No SaveChangesAsync: the transaction behaviour saves and commits once the handler succeeds.
        db.Invoices.Add(invoice);
        return new(invoice.Id);
    }
}

public sealed class GetInvoiceHandler(InvoicingDbContext db) : IQueryHandler<GetInvoice, InvoiceView?>
{
    public async ValueTask<InvoiceView?> HandleAsync(GetInvoice query, CancellationToken ct = default) =>
        await db.Invoices
            .Where(i => i.Id == query.Id)
            .Select(i => new InvoiceView(i.Id, i.Customer, i.Amount, i.Status, i.CreatedAt))
            .SingleOrDefaultAsync(ct);
}

public sealed class ListInvoicesHandler(InvoicingDbContext db) : IStreamQueryHandler<ListInvoices, InvoiceView>
{
    // Results are streamed as they're read, so hosts can send them before the query finishes.
    public async IAsyncEnumerable<InvoiceView> HandleAsync(ListInvoices query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var invoices = db.Invoices
            .OrderByDescending(i => i.Id) // version 7 ids sort by creation time
            .Take(query.Max)
            .Select(i => new InvoiceView(i.Id, i.Customer, i.Amount, i.Status, i.CreatedAt))
            .AsAsyncEnumerable();

        await foreach (var invoice in invoices.WithCancellation(ct))
        {
            yield return invoice;
        }
    }
}

public sealed class CancelInvoiceHandler(InvoicingDbContext db, IMediator mediator, IPrincipalAccessor principal) : ICommandHandler<CancelInvoice>
{
    public async ValueTask HandleAsync(CancelInvoice command, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new InvoiceNotFoundException();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return;
        }

        invoice.Status = InvoiceStatus.Cancelled;

        // Runs now, inside this command's transaction: if anything fails later, the audit entry is rolled back too.
        await mediator.PublishAsync(new InvoiceCancelled(invoice.Id, command.Reason, principal.Principal?.Identity?.Name ?? ""), ct);
    }
}

public sealed class AuditInvoiceCancelled(InvoicingDbContext db) : IMessageHandler<InvoiceCancelled>
{
    public ValueTask HandleAsync(InvoiceCancelled message, CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Description = $"Invoice {message.Id} cancelled by {message.CancelledBy}: {message.Reason}",
            At = DateTimeOffset.UtcNow,
        });
        return default;
    }
}

// An application exception. Each host turns it into a safe response through its error hook.
public sealed class InvoiceNotFoundException : Exception
{
    public InvoiceNotFoundException()
        : base("The invoice doesn't exist.")
    {
    }
}
