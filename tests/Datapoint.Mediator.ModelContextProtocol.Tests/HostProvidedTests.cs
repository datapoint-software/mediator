using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using global::ModelContextProtocol.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.ModelContextProtocol.Tests;

public sealed class HostProvidedTests
{
    private const string Refused = "'tenantId' is set by the server and cannot be given.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("reports_totals")]
    [InlineData("reports_balance")]
    public async Task Schema_LeavesThePropertyOut(string tool)
    {
        await using var mcp = await StartAsync();

        var schema = await SchemaAsync(mcp, tool);

        Assert.True(schema.GetProperty("properties").TryGetProperty("customer", out _));
        Assert.DoesNotContain("tenant", schema.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Schema_KeepsOtherRequiredProperties()
    {
        await using var mcp = await StartAsync();

        var schema = await SchemaAsync(mcp, "reports_totals");

        Assert.Equal(["customer"], schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()));
    }

    [Theory]
    [InlineData("reports_totals")]
    [InlineData("reports_balance")]
    public async Task CallTool_RunsWithoutTheProperty(string tool)
    {
        await using var mcp = await StartAsync();

        var result = await mcp.Client.CallToolAsync(tool, new Dictionary<string, object?> { ["customer"] = "Ana" }, cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal($"{Guid.Empty} Ana", result.StructuredContent!.Value.GetProperty("result").GetString());
    }

    [Theory]
    [InlineData("reports_totals", "tenantId")]
    [InlineData("reports_totals", "TenantId")]
    [InlineData("reports_totals", "TENANTID")]
    [InlineData("reports_balance", "tenantId")]
    [InlineData("reports_balance", "tENANTiD")]
    public async Task CallTool_WithTheProperty_IsRefused(string tool, string key)
    {
        await using var mcp = await StartAsync();

        var result = await mcp.Client.CallToolAsync(
            tool,
            new Dictionary<string, object?> { ["customer"] = "Ana", [key] = Guid.NewGuid() },
            cancellationToken: Ct);

        Assert.True(result.IsError);
        AssertRefused(Refused, key, Text(result));
        Assert.Empty(mcp.Recorder.Log);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("TENANT")]
    public async Task CallTool_WithARenamedProperty_IsRefusedByItsJsonName(string key)
    {
        await using var mcp = await StartAsync();

        var schema = await SchemaAsync(mcp, "reports_audit");
        var result = await mcp.Client.CallToolAsync(
            "reports_audit",
            new Dictionary<string, object?> { [key] = Guid.NewGuid() },
            cancellationToken: Ct);

        Assert.DoesNotContain("tenant", schema.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsError);
        AssertRefused("'tenant' is set by the server and cannot be given.", key, Text(result));
        Assert.Empty(mcp.Recorder.Log);
    }

    [Fact]
    public async Task DispatcherDecorator_SetsTheValue()
    {
        await using var mcp = await StartAsync(withDecorator: true);

        var result = await mcp.Client.CallToolAsync(
            "reports_totals",
            new Dictionary<string, object?> { ["customer"] = "Ana" },
            cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal($"{TenantDispatcher.TenantId} Ana", result.StructuredContent!.Value.GetProperty("result").GetString());
    }

    [Fact]
    public async Task DispatcherDecorator_DoesNotLetTheAgentChoose()
    {
        await using var mcp = await StartAsync(withDecorator: true);

        var result = await mcp.Client.CallToolAsync(
            "reports_totals",
            new Dictionary<string, object?> { ["customer"] = "Ana", ["tenantId"] = TenantDispatcher.TenantId },
            cancellationToken: Ct);

        Assert.True(result.IsError);
        AssertRefused(Refused, "tenantId", Text(result));
        Assert.Empty(mcp.Recorder.Log);
    }

    [Fact]
    public async Task ActionWithoutHostProvidedProperties_KeepsItsSchema()
    {
        await using var mcp = await StartAsync();

        var schema = await SchemaAsync(mcp, "invoices_create");

        Assert.True(schema.GetProperty("properties").TryGetProperty("customer", out _));
        Assert.True(schema.GetProperty("properties").TryGetProperty("amount", out _));
    }

    private static Task<McpHarness> StartAsync(bool withDecorator = false) =>
        McpHarness.StartAsync(
            configureMediator: o => o
                .AddHandler<TenantTotalsHandler>()
                .AddHandler<TenantBalanceHandler>()
                .AddHandler<TenantAuditHandler>(),
            configureServices: withDecorator
                ? services => services.AddScoped<ActionDispatcher>().AddScoped<IActionDispatcher, TenantDispatcher>()
                : null);

    private static async Task<JsonElement> SchemaAsync(McpHarness mcp, string tool) =>
        (await mcp.Client.ListToolsAsync(cancellationToken: Ct)).Single(t => t.ProtocolTool.Name == tool).ProtocolTool.InputSchema;

    // The SDK names the tool before the message. The message names the contract's property, never the agent's spelling.
    private static void AssertRefused(string expected, string key, string text)
    {
        Assert.EndsWith(expected, text, StringComparison.Ordinal);
        Assert.Equal(expected.Contains(key, StringComparison.Ordinal), text.Contains(key, StringComparison.Ordinal));
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}

[Action("reports.totals")]
public sealed record TenantTotals([HostProvided] Guid TenantId, string Customer) : IQuery<string>;

[Action("reports.balance")]
public sealed record TenantBalance(string Customer) : IQuery<string>
{
    [HostProvided(Reason = "The tenant of the caller's session.")]
    public Guid TenantId { get; init; }
}

[Action("reports.audit")]
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

// Sets the tenant after the tool has checked the agent's arguments, as an in-process MCP host does.
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
