using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;

namespace Datapoint.Mediator.Authorization.DependencyInjection.Tests;

[Action("invoice.approve")]
public sealed record ApproveInvoice(decimal Amount) : ICommand;

// Named, so it can be dispatched by name, but deliberately without an authorizer.
[Action("invoice.void")]
public sealed record VoidInvoice : ICommand;

public sealed record ArchiveInvoice : ICommand;

public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

public sealed class ApproveInvoiceHandler(Recorder recorder) : ICommandHandler<ApproveInvoice>
{
    public ValueTask HandleAsync(ApproveInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"approved {command.Amount}");
        return default;
    }
}

public sealed class VoidInvoiceHandler(Recorder recorder) : ICommandHandler<VoidInvoice>
{
    public ValueTask HandleAsync(VoidInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add("voided");
        return default;
    }
}

// Declared out of name order on purpose, to check that scanning sorts them.
public sealed class ZetaApproveAuthorizer : IAuthorizer<ApproveInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.AllowIf(principal.IsInRole("Finance")));
}

internal sealed class AlphaApproveAuthorizer : IAuthorizer<ApproveInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.AllowIf(action.Amount <= 1000));
}

public sealed class InvoiceAuthorizer : IAuthorizer<ApproveInvoice>, IAuthorizer<ArchiveInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) => new(AuthorizationDecision.Allow());

    public ValueTask<AuthorizationDecision> AuthorizeAsync(ArchiveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) => new(AuthorizationDecision.Allow());
}

public abstract class AbstractAuthorizer : IAuthorizer<ArchiveInvoice>
{
    public abstract ValueTask<AuthorizationDecision> AuthorizeAsync(ArchiveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default);
}

public sealed class GenericAuthorizer<T> : IAuthorizer<T>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(T action, ClaimsPrincipal principal, CancellationToken ct = default) => new(AuthorizationDecision.Allow());
}

public sealed class OtherBehavior : IBehavior;
