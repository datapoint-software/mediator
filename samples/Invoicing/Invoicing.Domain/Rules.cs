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

// Without an upper bound, any caller could read the whole table. Without a lower one, SQLite reads LIMIT -1 as "no limit".
public sealed class ListInvoicesValidator : AbstractValidator<ListInvoices>
{
    public ListInvoicesValidator()
    {
        // FluentValidation's default message for InclusiveBetween repeats the value the caller sent, so it's replaced.
        RuleFor(q => q.Max).InclusiveBetween(1, 100).WithMessage("'Max' must be between 1 and 100.");
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
    public ValueTask<AuthorizationDecision> AuthorizeAsync(CreateInvoice action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.AllowIf(principal.IsInRole(FinanceRole)));

    public ValueTask<AuthorizationDecision> AuthorizeAsync(CancelInvoice action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.AllowIf(principal.IsInRole(FinanceRole)));

    // Any signed-in user can read them. The behaviour has already refused anonymous callers.
    public ValueTask<AuthorizationDecision> AuthorizeAsync(GetInvoice action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.Allow());

    public ValueTask<AuthorizationDecision> AuthorizeAsync(ListInvoices action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.Allow());
}
