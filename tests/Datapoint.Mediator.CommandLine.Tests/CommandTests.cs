using System;
using System.CommandLine;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.CommandLine.Tests;

public sealed class CommandTests
{
    [Fact]
    public async Task Actions_BecomeNestedSubcommands()
    {
        await using var cli = new Cli();

        Assert.Equal(["invoices", "ping", "reports"], cli.Root.Subcommands.Select(c => c.Name).Order(StringComparer.Ordinal));
        var invoices = cli.Root.Subcommands.Single(c => c.Name == "invoices");
        Assert.Equal(["archive", "create", "get", "list", "ship"], invoices.Subcommands.Select(c => c.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Filter_LimitsTheCommands()
    {
        await using var cli = new Cli(o => o.Filter = a => a.Name == "ping");

        Assert.Equal(["ping"], cli.Root.Subcommands.Select(c => c.Name));
    }

    [Fact]
    public async Task Help_ShowsDescriptionsAndRequiredOptions()
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("invoices", "create", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("Creates an invoice for a customer.", output);
        Assert.Contains("The customer's name. (required)", output);
        Assert.Contains("--amount", output);
        Assert.Contains("--notes", output);
        Assert.Contains("--json", output);
    }

    [Fact]
    public async Task CommandWithResult_PrintsJsonResult()
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("invoices", "create", "--customer", "Ana", "--amount", "100");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal($"\"{CreateInvoiceHandler.Id}\"", output.Trim());
        Assert.Equal(["create Ana 100 -"], cli.Recorder.Log);
    }

    [Fact]
    public async Task OptionalOption_IsPassedWhenGiven()
    {
        await using var cli = new Cli();

        await cli.RunAsync("invoices", "create", "--customer", "Ana", "--amount", "5", "--notes", "urgent");

        Assert.Equal(["create Ana 5 urgent"], cli.Recorder.Log);
    }

    [Fact]
    public async Task CommandWithoutResult_PrintsNothing()
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("ping");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Empty(output);
    }

    [Fact]
    public async Task Query_PrintsJsonWithWebNaming()
    {
        await using var cli = new Cli();

        var (_, output, _) = await cli.RunAsync("invoices", "get", "--id", "3");

        Assert.Equal("""{"id":3,"customerName":"Ana"}""", output.Trim());
    }

    [Fact]
    public async Task StreamQuery_PrintsJsonLines()
    {
        await using var cli = new Cli();

        var (exitCode, output, _) = await cli.RunAsync("invoices", "list", "--count", "3");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal(["1", "2", "3"], output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task MissingRequiredOption_IsACommandLineError()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "create", "--amount", "100");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("--customer", error);
        Assert.Empty(cli.Recorder.Log);
    }

    [Fact]
    public async Task WrongOptionType_IsACommandLineError()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "create", "--customer", "Ana", "--amount", "lots");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("--amount", error);
    }

    [Fact]
    public async Task Json_ReplacesTheIndividualOptions()
    {
        await using var cli = new Cli();

        var (exitCode, _, _) = await cli.RunAsync("invoices", "ship", "--json", """{ "id": 1, "address": { "city": "Lisbon" } }""");

        Assert.Equal(ActionExitCodes.Success, exitCode);
        Assert.Equal(["ship 1 to Lisbon"], cli.Recorder.Log);
    }

    [Fact]
    public async Task JsonAndIndividualOptionsTogether_IsACommandLineError()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "get", "--id", "1", "--json", "{}");

        Assert.Equal(ActionExitCodes.InvalidCommandLine, exitCode);
        Assert.Contains("--json", error);
    }

    [Fact]
    public async Task MalformedJson_ExitsWithInvalidArguments()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "get", "--json", "{ secret-input");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Contains("not valid JSON", error);
        Assert.DoesNotContain("secret-input", error);
    }

    [Fact]
    public async Task InvalidArguments_ExitWithoutEchoingInput()
    {
        await using var cli = new Cli();

        var (exitCode, _, error) = await cli.RunAsync("invoices", "create", "--json", """{ "customer": "Ana", "amount": "secret-input" }""");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.DoesNotContain("secret-input", error);
    }

    [Fact]
    public async Task NoUser_ExitsWithAuthenticationRequired()
    {
        await using var cli = WithAuthorization(principal: null);

        var (exitCode, _, error) = await cli.RunAsync("invoices", "get", "--id", "1");

        Assert.Equal(ActionExitCodes.AuthenticationRequired, exitCode);
        Assert.Contains("Authentication is required.", error);
    }

    [Fact]
    public async Task ActionWithoutAuthorizer_ExitsWithAccessDenied()
    {
        await using var cli = WithAuthorization(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test")));

        var denied = await cli.RunAsync("invoices", "archive", "--id", "1");
        var allowed = await cli.RunAsync("invoices", "get", "--id", "1");

        Assert.Equal(ActionExitCodes.AccessDenied, denied.ExitCode);
        Assert.Contains("Access is denied.", denied.Error);
        Assert.Equal(ActionExitCodes.Success, allowed.ExitCode);
    }

    [Fact]
    public async Task DenialWithDetails_ExitsWithAccessDenied_WithoutTheDetails()
    {
        await using var cli = new Cli(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"))))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithDetails<ArchiveInvoice>>());

        var denied = await cli.RunAsync("invoices", "archive", "--id", "1");

        Assert.Equal(ActionExitCodes.AccessDenied, denied.ExitCode);
        Assert.DoesNotContain("TOP-SECRET", denied.Error);
        Assert.DoesNotContain("TOP-SECRET", denied.Output);
    }

    [Fact]
    public async Task DenialWithAnApplicationException_PrintsNoDetails()
    {
        await using var cli = new Cli(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"))))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithStepUp<ArchiveInvoice>>());

        var denied = await cli.RunAsync("invoices", "archive", "--id", "1");

        Assert.Equal(ActionExitCodes.UnexpectedError, denied.ExitCode);
        Assert.DoesNotContain("TOP-SECRET", denied.Output + denied.Error);
    }

    [Fact]
    public async Task UnexpectedError_PrintsNoDetails()
    {
        await using var cli = new Cli();

        var (exitCode, output, error) = await cli.RunAsync("reports", "fail");

        Assert.Equal(ActionExitCodes.UnexpectedError, exitCode);
        Assert.Equal("An unexpected error occurred.", error.Trim());
        Assert.DoesNotContain("secret internal detail", output + error);
    }

    [Fact]
    public async Task UnexpectedError_UsesErrorMessageWhenProvided()
    {
        await using var cli = new Cli(o => o.ErrorMessage = e => e is InvalidOperationException ? "The report couldn't be generated." : null);

        var (exitCode, _, error) = await cli.RunAsync("reports", "fail");

        Assert.Equal(ActionExitCodes.InvalidArguments, exitCode);
        Assert.Equal("The report couldn't be generated.", error.Trim());
    }

    [Fact]
    public async Task EachInvocation_RunsInItsOwnScope()
    {
        await using var cli = new Cli();

        await cli.RunAsync("invoices", "archive", "--id", "1");
        await cli.RunAsync("invoices", "archive", "--id", "2");

        var scopes = cli.Recorder.Log.Select(entry => entry.Split(' ').Last()).ToArray();
        Assert.Equal(2, scopes.Distinct().Count());
    }

    [Fact]
    public async Task AddActions_NullArguments_Throw()
    {
        await using var cli = new Cli();
        var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Equal("command", Assert.Throws<ArgumentNullException>(() => ((RootCommand)null!).AddActions(provider)).ParamName);
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => new RootCommand().AddActions(null!)).ParamName);
    }

    private static Cli WithAuthorization(ClaimsPrincipal? principal) =>
        new(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal))
                .AddSingleton<IAuthorizer<GetInvoice>, AllowAll<GetInvoice>>());
}
