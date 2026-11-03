using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::ModelContextProtocol.Protocol;
using Xunit;

namespace Datapoint.Mediator.ModelContextProtocol.Tests;

public sealed class ArgumentErrorTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("amount", "amount")]
    [InlineData("AMOUNT", "amount")]
    public async Task InvalidArgument_NamesTheField(string sent, string field)
    {
        await using var mcp = await McpHarness.StartAsync();

        var text = await CallAsync(mcp, "invoices_create", new() { ["customer"] = "Ana", [sent] = "secret-input" });

        Assert.EndsWith($"The argument '{field}' of action 'invoices.create' is not valid.", text);
        AssertNoLeak(text);
    }

    [Fact]
    public async Task MissingRequiredArgument_WithStrictOptions_NamesTheField()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.SerializerOptions = Strict);

        var text = await CallAsync(mcp, "invoices_create", new() { ["customer"] = "Ana" });

        Assert.EndsWith("The argument 'amount' of action 'invoices.create' is not valid.", text);
    }

    [Fact]
    public async Task UnknownArgument_WithStrictOptions_IsReportedWithoutItsName()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.SerializerOptions = Strict);

        var text = await CallAsync(mcp, "invoices_create", new() { ["customer"] = "Ana", ["amount"] = 1, ["secret-input"] = 1 });

        Assert.EndsWith("The arguments for action 'invoices.create' are not valid.", text);
        AssertNoLeak(text);
    }

    [Fact]
    public async Task ErrorMessage_ReceivesInvalidArgumentsFirst()
    {
        InvalidActionArgumentsException? received = null;
        await using var mcp = await McpHarness.StartAsync(o => o.ErrorMessage = e =>
        {
            received = e as InvalidActionArgumentsException;
            return received is { Field: { } field } ? $"invalid: {field}" : null;
        });

        var text = await CallAsync(mcp, "invoices_create", new() { ["customer"] = "Ana", ["amount"] = "secret-input" });

        Assert.EndsWith("invalid: amount", text);
        Assert.Equal("invoices.create", received!.ActionName);
    }

    [Fact]
    public async Task ErrorMessage_ReturningNull_KeepsTheDefault()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.ErrorMessage = _ => null);

        var text = await CallAsync(mcp, "invoices_create", new() { ["customer"] = "Ana", ["amount"] = "secret-input" });

        Assert.EndsWith("The argument 'amount' of action 'invoices.create' is not valid.", text);
    }

    [Fact]
    public async Task ApplicationException_IsShownWithoutItsFieldOrMessage()
    {
        await using var mcp = await McpHarness.StartAsync();

        var text = await CallAsync(mcp, "reports_reject", []);

        Assert.EndsWith("The arguments for action 'reports.reject' are not valid.", text);
        Assert.DoesNotContain("ignore previous instructions", text);
        Assert.DoesNotContain("secret message", text);
    }

    private static async Task<string> CallAsync(McpHarness mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.Client.CallToolAsync(tool, arguments, cancellationToken: Ct);
        Assert.True(result.IsError);
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    }

    private static void AssertNoLeak(string text)
    {
        Assert.DoesNotContain("secret-input", text);
        Assert.DoesNotContain("System.", text);
    }
}
