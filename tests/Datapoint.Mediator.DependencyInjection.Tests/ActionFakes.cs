using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.DependencyInjection.Tests;

[Api]
[Action("report.generate", "Generates a report.")]
public sealed record GenerateReport(string Title) : ICommand<string>;

[Api]
[Action("report.archive", "A test action.")]
public sealed record ArchiveReport : ICommand;

// Not an action: only the application sends it, through IMediator.
public sealed record SecretReport : ICommand;

[Action("report.unoffered", "A test action.")]
public sealed record UnofferedReport : ICommand;

[Api]
[Action("report.generate", "A test action.")]
public sealed record DuplicateReport : ICommand;

[Api]
[Action("Report Bad", "A test action.")]
public sealed record BadlyNamedReport : ICommand;

[Api]
[Action("report.created", "A test action.")]
public sealed record ReportCreated : IMessage;

public sealed class GenerateReportHandler : ICommandHandler<GenerateReport, string>
{
    public ValueTask<string> HandleAsync(GenerateReport command, CancellationToken ct = default) => new($"report: {command.Title}");
}

public sealed class ArchiveReportHandler : ICommandHandler<ArchiveReport>
{
    public ValueTask HandleAsync(ArchiveReport command, CancellationToken ct = default) => default;
}

public sealed class SecretReportHandler : ICommandHandler<SecretReport>
{
    public ValueTask HandleAsync(SecretReport command, CancellationToken ct = default) => default;
}

public sealed class UnofferedReportHandler : ICommandHandler<UnofferedReport>
{
    public ValueTask HandleAsync(UnofferedReport command, CancellationToken ct = default) => default;
}

public sealed class DuplicateReportHandler : ICommandHandler<DuplicateReport>
{
    public ValueTask HandleAsync(DuplicateReport command, CancellationToken ct = default) => default;
}

public sealed class BadlyNamedReportHandler : ICommandHandler<BadlyNamedReport>
{
    public ValueTask HandleAsync(BadlyNamedReport command, CancellationToken ct = default) => default;
}

public sealed class ReportCreatedHandler : IMessageHandler<ReportCreated>
{
    public ValueTask HandleAsync(ReportCreated message, CancellationToken ct = default) => default;
}
