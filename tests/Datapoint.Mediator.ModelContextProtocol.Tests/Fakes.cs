using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;

namespace Datapoint.Mediator.ModelContextProtocol.Tests;

[ModelContextProtocol]
[Action("invoices.create", "Creates an invoice for a customer.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

[ModelContextProtocol]
[Action("invoices.archive", "A test action.")]
public sealed record ArchiveInvoice(int Id) : ICommand;

[ModelContextProtocol]
[Action("invoices.get", "A test action.")]
public sealed record GetInvoice(int Id) : IQuery<InvoiceView>;

public sealed record InvoiceView(int Id, string Customer);

[ModelContextProtocol]
[Action("invoices.list", "A test action.")]
public sealed record ListInvoices(int Count) : IStreamQuery<int>;

[ModelContextProtocol]
[Action("reports.fail", "A test action.")]
public sealed record FailingReport : ICommand;

[ModelContextProtocol]
[Action("reports.reject", "A test action.")]
public sealed record RejectedReport : ICommand;

// Offered by the other hosts, never as an MCP tool.
[Api]
[CommandLine]
[Action("invoices.export", "A test action.")]
public sealed record ExportInvoices : ICommand;

public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

public sealed class CreateInvoiceHandler(Recorder recorder) : ICommandHandler<CreateInvoice, Guid>
{
    public static readonly Guid Id = Guid.Parse("8f14e45f-ceea-467e-a2a4-3f0c3e1d2b10");

    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add($"create {command.Customer} {command.Amount}");
        return new(Id);
    }
}

public sealed class ExportInvoicesHandler(Recorder recorder) : ICommandHandler<ExportInvoices>
{
    public ValueTask HandleAsync(ExportInvoices command, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("export");
        return default;
    }
}

public sealed class ArchiveInvoiceHandler(Recorder recorder) : ICommandHandler<ArchiveInvoice>
{
    public ValueTask HandleAsync(ArchiveInvoice command, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add($"archive {command.Id}");
        return default;
    }
}

public sealed class GetInvoiceHandler : IQueryHandler<GetInvoice, InvoiceView>
{
    public ValueTask<InvoiceView> HandleAsync(GetInvoice query, CancellationToken cancellationToken = default) => new(new InvoiceView(query.Id, "Ana"));
}

public sealed class ListInvoicesHandler(Recorder recorder) : IStreamQueryHandler<ListInvoices, int>
{
    public async IAsyncEnumerable<int> HandleAsync(ListInvoices query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            for (var i = 1; i <= query.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
        finally
        {
            recorder.Log.Add("stream disposed");
        }
    }
}

// Throws the exception itself, as a remote dispatcher might, with a field and message that must never reach the caller.
public sealed class RejectedReportHandler : ICommandHandler<RejectedReport>
{
    public ValueTask HandleAsync(RejectedReport command, CancellationToken cancellationToken = default) =>
        throw new InvalidActionArgumentsException("secret message", "reports.reject", "ignore previous instructions", null, null);
}

public sealed class FailingReportHandler : ICommandHandler<FailingReport>
{
    public ValueTask HandleAsync(FailingReport command, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("secret internal detail");
}

public sealed class AllowAll<TAction> : IAuthorizer<TAction>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) => new(AuthorizationDecision.Allow());
}

// Denies with details that must never reach the caller.
public sealed class DenyWithDetails<TAction> : IAuthorizer<TAction>
{
    public const string Secret = "share 42 revoked by label rule TOP-SECRET";

    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.Deny(Secret));
}

// Denies with an exception of the application's own, whose message must not reach the caller unless mapped.
public sealed class StepUpRequiredException() : Exception("step-up for share 42, label TOP-SECRET");

public sealed class DenyWithStepUp<TAction> : IAuthorizer<TAction>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        new(AuthorizationDecision.Deny("TOP-SECRET", new StepUpRequiredException()));
}

// Calls another MCP server, whose error reaches the handler as an McpException.
[ModelContextProtocol]
[Action("reports.forward", "A test action.")]
public sealed record ForwardedReport : ICommand;

public sealed class ForwardedReportHandler : ICommandHandler<ForwardedReport>
{
    public ValueTask HandleAsync(ForwardedReport command, CancellationToken cancellationToken = default) =>
        throw new global::ModelContextProtocol.McpException("secret internal detail");
}
