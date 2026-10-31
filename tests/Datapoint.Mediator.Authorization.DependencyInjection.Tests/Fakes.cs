using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;

namespace Datapoint.Mediator.Authorization.DependencyInjection.Tests;

public sealed record ApproveInvoice(decimal Amount) : ICommand;

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

// Declared out of name order on purpose, to check that scanning sorts them.
public sealed class ZetaApproveAuthorizer : IAuthorizer<ApproveInvoice>
{
    public ValueTask<bool> IsAuthorizedAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(principal.IsInRole("Finance"));
}

internal sealed class AlphaApproveAuthorizer : IAuthorizer<ApproveInvoice>
{
    public ValueTask<bool> IsAuthorizedAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(action.Amount <= 1000);
}

public sealed class InvoiceAuthorizer : IAuthorizer<ApproveInvoice>, IAuthorizer<ArchiveInvoice>
{
    public ValueTask<bool> IsAuthorizedAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) => new(true);

    public ValueTask<bool> IsAuthorizedAsync(ArchiveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) => new(true);
}

public abstract class AbstractAuthorizer : IAuthorizer<ArchiveInvoice>
{
    public abstract ValueTask<bool> IsAuthorizedAsync(ArchiveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default);
}

public sealed class GenericAuthorizer<T> : IAuthorizer<T>
{
    public ValueTask<bool> IsAuthorizedAsync(T action, ClaimsPrincipal principal, CancellationToken ct = default) => new(true);
}

public sealed class OtherBehavior : IBehavior;

public sealed class FixedPrincipalAccessor(ClaimsPrincipal? principal) : IPrincipalAccessor
{
    public ClaimsPrincipal? Principal => principal;
}
