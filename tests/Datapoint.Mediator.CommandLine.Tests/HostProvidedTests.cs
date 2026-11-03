using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.CommandLine.Tests;

public sealed class HostProvidedTests
{
    private const string Refused = "'tenantId' is set by the server and cannot be given.";

    [Theory]
    [InlineData("totals")]
    [InlineData("balance")]
    public async Task Help_HasNoOptionForTheProperty(string command)
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("reports", command, "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--customer", output);
        Assert.DoesNotContain("tenant", output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("totals")]
    [InlineData("balance")]
    public async Task Command_RunsWithoutTheProperty(string command)
    {
        await using var cli = new Cli();

        var (exitCode, output, error) = await cli.RunAsync("reports", command, "--customer", "Ana");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Empty(error);
        Assert.Equal($"\"{Guid.Empty} Ana\"", output.Trim());
    }

    [Fact]
    public async Task Option_ForTheProperty_IsUnknown()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("reports", "totals", "--customer", "Ana", "--tenant-id", Guid.NewGuid().ToString());

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("--tenant-id", error);
        Assert.Empty(cli.Recorder.Log);
    }

    [Theory]
    [InlineData("totals", "tenantId")]
    [InlineData("totals", "TenantId")]
    [InlineData("totals", "TENANTID")]
    [InlineData("balance", "tenantId")]
    [InlineData("balance", "tENANTiD")]
    public async Task Json_WithTheProperty_IsRefused(string command, string key)
    {
        await using var cli = new Cli();

        var (exitCode, output, error) = await cli.RunAsync(
            "reports", command, "--json", $$"""{"customer":"Ana","{{key}}":"{{Guid.NewGuid()}}"}""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Empty(output);
        Assert.Equal(Refused, error.Trim());
        Assert.Empty(cli.Recorder.Log);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("TENANT")]
    public async Task Json_WithARenamedProperty_IsRefusedByItsJsonName(string key)
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("reports", "audit", "--json", $$"""{"{{key}}":"{{Guid.NewGuid()}}"}""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal("'tenant' is set by the server and cannot be given.", error.Trim());
        Assert.Empty(cli.Recorder.Log);
    }

    [Fact]
    public async Task Json_WithoutTheProperty_Runs()
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("reports", "totals", "--json", """{"customer":"Ana"}""");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal($"\"{Guid.Empty} Ana\"", output.Trim());
    }

    [Theory]
    [InlineData("--customer", "Ana")]
    [InlineData("--json", """{"customer":"Ana"}""")]
    public async Task DispatcherDecorator_SetsTheValue(string option, string value)
    {
        await using var cli = new Cli(configureServices: services => services
            .AddScoped<ActionDispatcher>()
            .AddScoped<IActionDispatcher, TenantDispatcher>());

        var (exitCode, output, error) = await cli.RunAsync("reports", "totals", option, value);

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Empty(error);
        Assert.Equal($"\"{TenantDispatcher.TenantId} Ana\"", output.Trim());
    }

    [Fact]
    public async Task DispatcherDecorator_DoesNotLetTheCallerChoose()
    {
        await using var cli = new Cli(configureServices: services => services
            .AddScoped<ActionDispatcher>()
            .AddScoped<IActionDispatcher, TenantDispatcher>());

        var (exitCode, _, error) = await cli.RunAsync(
            "reports", "totals", "--json", $$"""{"customer":"Ana","tenantId":"{{TenantDispatcher.TenantId}}"}""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal(Refused, error.Trim());
    }

    [Fact]
    public async Task ActionWithoutHostProvidedProperties_KeepsEveryOption()
    {
        await using var cli = new Cli();

        var (_, output, _) = await cli.RunAsync("invoices", "create", "--help");
        var (exitCode, _, _) = await cli.RunAsync("invoices", "create", "--json", """{"customer":"Ana","amount":1,"notes":"x"}""");

        Assert.Contains("--customer", output);
        Assert.Contains("--amount", output);
        Assert.Contains("--notes", output);
        Assert.Equal(ActionExitCodes.Success, exitCode);
    }
}

[Action("reports.totals", "A test action.")]
public sealed record TenantTotals([HostProvided] Guid TenantId, string Customer) : IQuery<string>;

[Action("reports.balance", "A test action.")]
public sealed record TenantBalance(string Customer) : IQuery<string>
{
    [HostProvided(Reason = "The tenant of the caller's session.")]
    public Guid TenantId { get; init; }
}

[Action("reports.audit", "A test action.")]
public sealed record TenantAudit : ICommand
{
    [HostProvided]
    [JsonPropertyName("tenant")]
    public Guid TenantId { get; init; }
}

public sealed class TenantTotalsHandler(Recorder recorder) : IQueryHandler<TenantTotals, string>
{
    public ValueTask<string> HandleAsync(TenantTotals query, CancellationToken ct = default)
    {
        recorder.Log.Add("totals");
        return new($"{query.TenantId} {query.Customer}");
    }
}

public sealed class TenantBalanceHandler(Recorder recorder) : IQueryHandler<TenantBalance, string>
{
    public ValueTask<string> HandleAsync(TenantBalance query, CancellationToken ct = default)
    {
        recorder.Log.Add("balance");
        return new($"{query.TenantId} {query.Customer}");
    }
}

public sealed class TenantAuditHandler(Recorder recorder) : ICommandHandler<TenantAudit>
{
    public ValueTask HandleAsync(TenantAudit command, CancellationToken ct = default)
    {
        recorder.Log.Add("audit");
        return default;
    }
}

// Sets the tenant after the adapter has checked the caller's arguments, as an in-process host does.
public sealed class TenantDispatcher(ActionDispatcher inner) : IActionDispatcher
{
    public static readonly Guid TenantId = Guid.Parse("5d41402a-bc4b-4a76-b971-9d911017c592");

    public ValueTask<object?> DispatchAsync(string name, JsonElement arguments, JsonSerializerOptions? options = null, CancellationToken ct = default)
    {
        var json = JsonObject.Create(arguments)!;
        json["tenantId"] = TenantId;
        return inner.DispatchAsync(name, JsonSerializer.SerializeToElement(json), options, ct);
    }

    public IAsyncEnumerable<object?> StreamAsync(string name, JsonElement arguments, JsonSerializerOptions? options = null, CancellationToken ct = default) =>
        inner.StreamAsync(name, arguments, options, ct);
}
