using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.DependencyInjection.Tests;

[Action("report.generate", Description = "Generates a report.")]
public sealed record GenerateReport(string Title) : ICommand<string>;

[Action("report.archive")]
public sealed record ArchiveReport : ICommand;

[Action("report.secret", Exposed = false)]
public sealed record SecretReport : ICommand;

[Action("report.generate")]
public sealed record DuplicateReport : ICommand;

[Action("Report Bad")]
public sealed record BadlyNamedReport : ICommand;

[Action("report.created")]
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
