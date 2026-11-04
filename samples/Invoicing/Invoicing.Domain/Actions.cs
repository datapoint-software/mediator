using System.ComponentModel;
using Datapoint.Mediator;

namespace Invoicing.Domain;

// Each action is marked with the hosts that offer it, here all three: POST /api/invoices/create,
// `invoicing invoices create`, and the MCP tool invoices_create. Descriptions become OpenAPI summaries, CLI help and the text an AI agent reads to choose a tool.

[Api]
[CommandLine]
[ModelContextProtocol]
[Action("invoices.create", "Creates an open invoice for a customer and returns its id.")]
public sealed record CreateInvoice(
    [property: Description("The customer's name.")] string Customer,
    [property: Description("The amount to invoice, greater than zero.")] decimal Amount) : ICommand<Guid>;

[Api]
[CommandLine]
[ModelContextProtocol]
[Action("invoices.get", "Gets an invoice by id, or null when it doesn't exist.")]
public sealed record GetInvoice(
    [property: Description("The invoice's id.")] Guid Id) : IQuery<InvoiceView?>;

[Api]
[CommandLine]
[ModelContextProtocol]
[Action("invoices.list", "Lists invoices, newest first.")]
public sealed record ListInvoices(
    [property: Description("The maximum number of invoices to return, from 1 to 100.")] int Max = 50) : IStreamQuery<InvoiceView>;

[Api]
[CommandLine]
[ModelContextProtocol]
[Action("invoices.cancel", "Cancels an open invoice.")]
public sealed record CancelInvoice(
    [property: Description("The invoice's id.")] Guid Id,
    [property: Description("Why the invoice is cancelled.")] string Reason) : ICommand;

public sealed record InvoiceView(Guid Id, string Customer, decimal Amount, InvoiceStatus Status, DateTimeOffset CreatedAt);

// Published by CancelInvoice's handler. Messages aren't actions and aren't authorized, so they're only ever published
// by the application's own code, after the command that publishes them was authorized.
public sealed record InvoiceCancelled(Guid Id, string Reason, string CancelledBy) : IMessage;
