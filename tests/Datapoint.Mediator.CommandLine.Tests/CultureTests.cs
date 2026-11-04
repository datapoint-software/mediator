using System;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Datapoint.Mediator.CommandLine.Tests;

// Runs on a machine set to Portuguese, which writes "42,5" and "01/11/2026", to check that options don't depend on it.
public sealed class CultureTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-PT");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-PT");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    [Fact]
    public async Task Decimal_IsParsedWithTheInvariantCulture()
    {
        await using var cli = new Cli();

        var (exitCode, _, _) = await cli.RunAsync("invoices", "create", "--customer", "Ana", "--amount", "42.5");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal(["create Ana 42.5 -"], cli.Recorder.Log);
    }

    [Fact]
    public async Task Decimal_InTheCurrentCulturesFormat_IsInvalid()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "create", "--customer", "Ana", "--amount", "42,5");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("Option '--amount' expects a number, with '.' as the decimal separator and no thousands separators.", error);
        Assert.DoesNotContain("42,5", error);
        Assert.DoesNotContain("System.", error);
        Assert.Empty(cli.Recorder.Log);
    }

    [Fact]
    public async Task Integer_WithAThousandsSeparator_IsInvalid()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "archive", "--id", "1,000");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("Option '--id' expects a whole number, with no thousands separators.", error);
        Assert.DoesNotContain("1,000", error);
        Assert.DoesNotContain("System.", error);
        Assert.Empty(cli.Recorder.Log);
    }

    [Fact]
    public async Task DatesAndNullableNumbers_AreParsedWithTheInvariantCulture()
    {
        await using var cli = new Cli();

        var (exitCode, _, _) = await cli.RunAsync(
            "reports", "schedule", "--day", "2026-11-01", "--threshold", "0.75", "--notify-at", "2026-11-01T09:30:00+00:00");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal(["schedule 2026-11-01 0.75 2026-11-01T09:30:00.0000000+00:00"], cli.Recorder.Log);
    }

    [Fact]
    public async Task NullableOptions_WhenAbsent_StayNull()
    {
        await using var cli = new Cli();

        var (exitCode, _, _) = await cli.RunAsync("reports", "schedule", "--day", "2026-11-01");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal(["schedule 2026-11-01 - -"], cli.Recorder.Log);
    }

    [Fact]
    public async Task NullableOption_WithAnInvalidValue_IsInvalid()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("reports", "schedule", "--day", "2026-11-01", "--threshold", "0,75");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("Option '--threshold' expects a number", error);
        Assert.DoesNotContain("0,75", error);
        Assert.Empty(cli.Recorder.Log);
    }

    [Fact]
    public async Task UnparsableDate_IsInvalid()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("reports", "schedule", "--day", "tomorrow");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("Option '--day' has a value that isn't valid. Write dates and times in ISO 8601, such as 2026-11-01.", error);
        Assert.DoesNotContain("tomorrow", error);
        Assert.DoesNotContain("System.", error);
    }
}
