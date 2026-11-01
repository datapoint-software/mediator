using System.Security.Claims;
using Datapoint.Mediator.Authorization;
using FluentValidation;

namespace Invoicing.Domain;

public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoice>
{
    public CreateInvoiceValidator()
    {
        RuleFor(c => c.Customer).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Amount).GreaterThan(0);
    }
}

public sealed class CancelInvoiceValidator : AbstractValidator<CancelInvoice>
{
    public CancelInvoiceValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

// Authorization fails closed: an action without an authorizer is denied. Every action here has one.
public sealed class InvoiceAuthorizer :
    IAuthorizer<CreateInvoice>,
    IAuthorizer<CancelInvoice>,
    IAuthorizer<GetInvoice>,
    IAuthorizer<ListInvoices>
{
    public const string FinanceRole = "Finance";

    // Changing invoices needs the Finance role.
    public ValueTask<bool> IsAuthorizedAsync(CreateInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(principal.IsInRole(FinanceRole));

    public ValueTask<bool> IsAuthorizedAsync(CancelInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(principal.IsInRole(FinanceRole));

    // Any signed-in user can read them. The behaviour has already refused anonymous callers.
    public ValueTask<bool> IsAuthorizedAsync(GetInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(true);

    public ValueTask<bool> IsAuthorizedAsync(ListInvoices action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(true);
}
