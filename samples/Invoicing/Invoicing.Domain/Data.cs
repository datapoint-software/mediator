using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Invoicing.Domain;

// Serialized as "Open" or "Cancelled" by every host, rather than as a number.
[JsonConverter(typeof(JsonStringEnumConverter<InvoiceStatus>))]
public enum InvoiceStatus
{
    Open,
    Cancelled,
}

public sealed class Invoice
{
    public Guid Id { get; init; }

    public required string Customer { get; init; }

    public decimal Amount { get; init; }

    public InvoiceStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public string CreatedBy { get; init; } = "";
}

// Written by a message handler, inside the same transaction as the command that published the message.
public sealed class AuditEntry
{
    public int Id { get; init; }

    public required string Description { get; init; }

    public DateTimeOffset At { get; init; }
}

public sealed class InvoicingDbContext(DbContextOptions<InvoicingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
}
