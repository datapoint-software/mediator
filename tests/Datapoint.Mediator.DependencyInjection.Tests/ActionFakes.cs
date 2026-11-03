using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.DependencyInjection.Tests;

[Action("report.generate", "Generates a report.")]
public sealed record GenerateReport(string Title) : ICommand<string>;

[Action("report.archive", "A test action.")]
public sealed record ArchiveReport : ICommand;

[Action("report.secret", "A test action.", Hidden = true)]
public sealed record SecretReport : ICommand;

[Action("report.generate", "A test action.")]
public sealed record DuplicateReport : ICommand;

[Action("Report Bad", "A test action.")]
public sealed record BadlyNamedReport : ICommand;

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
