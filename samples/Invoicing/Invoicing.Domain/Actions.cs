using System.ComponentModel;
using Datapoint.Mediator;

namespace Invoicing.Domain;

// Each action is offered by every host: POST /api/invoices/create, `invoicing invoices create`, and the MCP tool
// invoices_create. Descriptions become OpenAPI summaries, CLI help and the text an AI agent reads to choose a tool.

[Action("invoices.create", "Creates an open invoice for a customer and returns its id.")]
public sealed record CreateInvoice(
    [property: Description("The customer's name.")] string Customer,
    [property: Description("The amount to invoice, greater than zero.")] decimal Amount) : ICommand<Guid>;

[Action("invoices.get", "Gets an invoice by id, or null when it doesn't exist.")]
public sealed record GetInvoice(
    [property: Description("The invoice's id.")] Guid Id) : IQuery<InvoiceView?>;

[Action("invoices.list", "Lists invoices, newest first.")]
public sealed record ListInvoices(
    [property: Description("The maximum number of invoices to return.")] int Max = 50) : IStreamQuery<InvoiceView>;

[Action("invoices.cancel", "Cancels an open invoice.")]
public sealed record CancelInvoice(
    [property: Description("The invoice's id.")] Guid Id,
    [property: Description("Why the invoice is cancelled.")] string Reason) : ICommand;

public sealed record InvoiceView(Guid Id, string Customer, decimal Amount, InvoiceStatus Status, DateTimeOffset CreatedAt);

// Published by CancelInvoice's handler. Messages aren't actions and aren't authorized, so they're only ever published
// by the application's own code, after the command that publishes them was authorized.
public sealed record InvoiceCancelled(Guid Id, string Reason, string CancelledBy) : IMessage;
