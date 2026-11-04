using System.Runtime.CompilerServices;
using Datapoint.Mediator;
using Datapoint.Mediator.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Invoicing.Domain;

public sealed class CreateInvoiceHandler(InvoicingDbContext db, IPrincipalAccessor principal, TimeProvider time)
    : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken cancellationToken = default)
    {
        var invoice = new Invoice
        {
            // Generated here, because database-generated keys are only known after the save.
            Id = Guid.CreateVersion7(),
            Customer = command.Customer,
            Amount = command.Amount,
            Status = InvoiceStatus.Open,
            CreatedAt = time.GetUtcNow(),
            CreatedBy = principal.RequireUserName(),
        };

        // No SaveChangesAsync: the transaction behaviour saves and commits once the handler succeeds.
        db.Invoices.Add(invoice);
        return new(invoice.Id);
    }
}

public sealed class GetInvoiceHandler(InvoicingDbContext db) : IQueryHandler<GetInvoice, InvoiceView?>
{
    public async ValueTask<InvoiceView?> HandleAsync(GetInvoice query, CancellationToken cancellationToken = default) =>
        await db.Invoices
            .Where(i => i.Id == query.Id)
            .Select(i => new InvoiceView(i.Id, i.Customer, i.Amount, i.Status, i.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed class ListInvoicesHandler(InvoicingDbContext db) : IStreamQueryHandler<ListInvoices, InvoiceView>
{
    // Results are streamed as they're read, so hosts can send them before the query finishes.
    public async IAsyncEnumerable<InvoiceView> HandleAsync(ListInvoices query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var invoices = db.Invoices
            // Version 7 ids sort by creation time.
            .OrderByDescending(i => i.Id)
            .Take(query.Max)
            .Select(i => new InvoiceView(i.Id, i.Customer, i.Amount, i.Status, i.CreatedAt))
            .AsAsyncEnumerable();

        await foreach (var invoice in invoices.WithCancellation(cancellationToken))
        {
            yield return invoice;
        }
    }
}

public sealed class CancelInvoiceHandler(InvoicingDbContext db, IMediator mediator, IPrincipalAccessor principal)
    : ICommandHandler<CancelInvoice>
{
    public async ValueTask HandleAsync(CancelInvoice command, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == command.Id, cancellationToken)
            ?? throw new InvoiceNotFoundException();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return;
        }

        invoice.Status = InvoiceStatus.Cancelled;

        // Runs now, inside this command's transaction: if anything fails later, the audit entry is rolled back too.
        await mediator.PublishAsync(new InvoiceCancelled(invoice.Id, command.Reason, principal.RequireUserName()), cancellationToken);
    }
}

public sealed class AuditInvoiceCancelled(InvoicingDbContext db, TimeProvider time) : IMessageHandler<InvoiceCancelled>
{
    public ValueTask HandleAsync(InvoiceCancelled message, CancellationToken cancellationToken = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Description = $"Invoice {message.Id} cancelled by {message.CancelledBy}: {message.Reason}",
            At = time.GetUtcNow(),
        });
        return default;
    }
}

internal static class PrincipalAccessorExtensions
{
    // The authorization behaviour has already refused anonymous callers, so a missing name is a misconfigured identity
    // provider. Recording an empty user would hide that, so it fails instead.
    public static string RequireUserName(this IPrincipalAccessor accessor)
    {
        var name = accessor.Principal?.Identity?.Name;

        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("The signed-in user has no name, so the change can't be recorded against them.");
        }

        return name;
    }
}
