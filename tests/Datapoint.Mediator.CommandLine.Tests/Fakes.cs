using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;

namespace Datapoint.Mediator.CommandLine.Tests;

[Action("invoices.create", Description = "Creates an invoice for a customer.")]
public sealed record CreateInvoice([property: Description("The customer's name.")] string Customer, decimal Amount, string? Notes = null) : ICommand<Guid>;

[Action("invoices.archive")]
public sealed record ArchiveInvoice(int Id) : ICommand;

[Action("invoices.get")]
public sealed record GetInvoice(int Id) : IQuery<InvoiceView>;

public sealed record InvoiceView(int Id, string CustomerName);

[Action("invoices.list")]
public sealed record ListInvoices(int Count) : IStreamQuery<int>;

[Action("invoices.ship")]
public sealed record ShipInvoice(int Id, Address Address) : ICommand;

public sealed record Address(string City);

[Action("reports.fail")]
public sealed record FailingReport : ICommand;

[Action("ping")]
public sealed record Ping : ICommand;

public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

// A scoped service, to observe one DI scope per invocation.
public sealed class ScopedMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class CreateInvoiceHandler(Recorder recorder) : ICommandHandler<CreateInvoice, Guid>
{
    public static readonly Guid Id = Guid.Parse("8f14e45f-ceea-467e-a2a4-3f0c3e1d2b10");

    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"create {command.Customer} {command.Amount} {command.Notes ?? "-"}");
        return new(Id);
    }
}

public sealed class ArchiveInvoiceHandler(Recorder recorder, ScopedMarker marker) : ICommandHandler<ArchiveInvoice>
{
    public ValueTask HandleAsync(ArchiveInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"archive {command.Id} in scope {marker.Id}");
        return default;
    }
}

public sealed class GetInvoiceHandler : IQueryHandler<GetInvoice, InvoiceView>
{
    public ValueTask<InvoiceView> HandleAsync(GetInvoice query, CancellationToken ct = default) => new(new InvoiceView(query.Id, "Ana"));
}

public sealed class ListInvoicesHandler : IStreamQueryHandler<ListInvoices, int>
{
    public async IAsyncEnumerable<int> HandleAsync(ListInvoices query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= query.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

public sealed class ShipInvoiceHandler(Recorder recorder) : ICommandHandler<ShipInvoice>
{
    public ValueTask HandleAsync(ShipInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"ship {command.Id} to {command.Address.City}");
        return default;
    }
}

public sealed class FailingReportHandler : ICommandHandler<FailingReport>
{
    public ValueTask HandleAsync(FailingReport command, CancellationToken ct = default) =>
        throw new InvalidOperationException("secret internal detail");
}

public sealed class PingHandler : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken ct = default) => default;
}

public sealed class AllowAll<TAction> : IAuthorizer<TAction>
{
    public ValueTask<bool> IsAuthorizedAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default) => new(true);
}
