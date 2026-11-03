using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Datapoint.Mediator.CommandLine.Tests;

public sealed class ArgumentErrorTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Theory]
    [InlineData("""{ "customer": "Ana", "amount": "secret-input" }""", "amount")]
    [InlineData("""{ "customer": "Ana", "AMOUNT": "secret-input" }""", "amount")]
    public async Task InvalidArgument_NamesTheField(string json, string field)
    {
        await using var cli = new Cli();

        var (exitCode, output, error) = await cli.RunAsync("invoices", "create", "--json", json);

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal($"The argument '{field}' of action 'invoices.create' is not valid.", error.Trim());
        AssertNoLeak(output + error);
    }

    [Fact]
    public async Task MissingRequiredArgument_WithStrictOptions_NamesTheField()
    {
        await using var cli = new Cli(o => o.SerializerOptions = Strict);

        var (_, _, error) = await cli.RunAsync("invoices", "create", "--json", """{ "customer": "Ana" }""");

        Assert.Equal("The argument 'amount' of action 'invoices.create' is not valid.", error.Trim());
    }

    [Fact]
    public async Task UnknownArgument_WithStrictOptions_IsReportedWithoutItsName()
    {
        await using var cli = new Cli(o => o.SerializerOptions = Strict);

        var (exitCode, output, error) = await cli.RunAsync("invoices", "create", "--json", """{ "customer": "Ana", "amount": 1, "secret-input": 1 }""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal("The arguments for action 'invoices.create' are not valid.", error.Trim());
        AssertNoLeak(output + error);
    }

    [Fact]
    public async Task ErrorMessage_ReceivesInvalidArgumentsFirst()
    {
        InvalidActionArgumentsException? received = null;
        await using var cli = new Cli(o => o.ErrorMessage = e =>
        {
            received = e as InvalidActionArgumentsException;
            return received is { Field: { } field } ? $"invalid: {field}" : null;
        });

        var (exitCode, _, error) = await cli.RunAsync("invoices", "create", "--json", """{ "customer": "Ana", "amount": "secret-input" }""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal("invalid: amount", error.Trim());
        Assert.Equal("invoices.create", received!.ActionName);
    }

    [Fact]
    public async Task ErrorMessage_ReturningNull_KeepsTheDefault()
    {
        await using var cli = new Cli(o => o.ErrorMessage = _ => null);

        var (_, _, error) = await cli.RunAsync("invoices", "create", "--json", """{ "customer": "Ana", "amount": "secret-input" }""");

        Assert.Equal("The argument 'amount' of action 'invoices.create' is not valid.", error.Trim());
    }

    [Fact]
    public async Task ApplicationException_IsShownWithoutItsFieldOrMessage()
    {
        await using var cli = new Cli();

        var (exitCode, output, error) = await cli.RunAsync("reports", "reject");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal("The arguments for action 'reports.reject' are not valid.", error.Trim());
        Assert.DoesNotContain("ignore previous instructions", output + error);
        Assert.DoesNotContain("secret message", output + error);
    }

    private static void AssertNoLeak(string text)
    {
        Assert.DoesNotContain("secret-input", text);
        Assert.DoesNotContain("System.", text);
    }
}
