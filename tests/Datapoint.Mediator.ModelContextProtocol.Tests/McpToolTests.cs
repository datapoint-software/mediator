using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;
using global::ModelContextProtocol.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using global::ModelContextProtocol.Server;
using Xunit;

namespace Datapoint.Mediator.ModelContextProtocol.Tests;

public sealed class McpToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListTools_OffersEveryActionWithNamesDescriptionsSchemasAndHints()
    {
        await using var mcp = await McpHarness.StartAsync();

        var tools = (await mcp.Client.ListToolsAsync(cancellationToken: Ct)).ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool);

        Assert.Equal(
            ["invoices_archive", "invoices_create", "invoices_get", "invoices_list", "reports_fail", "reports_reject"],
            tools.Keys.Order(StringComparer.Ordinal));

        var create = tools["invoices_create"];
        Assert.Equal("Creates an invoice for a customer.", create.Description);
        Assert.Equal("object", create.InputSchema.GetProperty("type").GetString());
        var properties = create.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("customer", out _));
        Assert.True(properties.TryGetProperty("amount", out _));
        Assert.False(create.Annotations?.ReadOnlyHint);
        Assert.True(create.Annotations?.DestructiveHint);

        Assert.True(tools["invoices_get"].Annotations?.ReadOnlyHint);
        Assert.Null(tools["invoices_get"].Annotations?.DestructiveHint);
        Assert.True(tools["invoices_list"].Annotations?.ReadOnlyHint);
    }

    [Fact]
    public async Task ActionNotMarkedForMcp_IsNotOfferedAndFailsLikeAnUnknownTool()
    {
        await using var mcp = await McpHarness.StartAsync();

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);
        var notOffered = await Record.ExceptionAsync(async () => await mcp.Client.CallToolAsync("invoices_export", cancellationToken: Ct));
        var unknown = await Record.ExceptionAsync(async () => await mcp.Client.CallToolAsync("invoices_unknown", cancellationToken: Ct));

        Assert.DoesNotContain("invoices_export", tools.Select(t => t.ProtocolTool.Name));
        Assert.NotNull(notOffered);
        Assert.Equal(unknown!.GetType(), notOffered.GetType());
        Assert.Equal(unknown.Message.Replace("invoices_unknown", "invoices_export"), notOffered.Message);
        Assert.DoesNotContain("export", mcp.Recorder.Log);
    }

    [Fact]
    public async Task Filter_CannotOfferAnActionNotMarkedForMcp()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.Filter = _ => true);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);

        Assert.DoesNotContain("invoices_export", tools.Select(t => t.ProtocolTool.Name));
    }

    [Fact]
    public async Task Filter_LimitsTheOfferedTools()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.Filter = a => a.Kind == ActionKind.Query);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(["invoices_get"], tools.Select(t => t.ProtocolTool.Name));
    }

    [Fact]
    public async Task WithMediatorActionsCalledTwice_DoesNotDuplicateTools()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>()
            .AddMediator(o => o.AddHandler<GetInvoiceHandler>());
        services.AddMcpServer().WithMediatorActions().WithMediatorActions();
        await using var provider = services.BuildServiceProvider();

        var tools = provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!;

        Assert.Single(tools);
    }

    [Fact]
    public async Task CallTool_CommandWithResult_ReturnsStructuredAndTextResult()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync(
            "invoices_create",
            new Dictionary<string, object?> { ["customer"] = "Ana", ["amount"] = 100 },
            cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(CreateInvoiceHandler.Id.ToString(), result.StructuredContent!.Value.GetProperty("result").GetString());
        Assert.Equal(result.StructuredContent.Value.GetRawText(), Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(["create Ana 100"], mcp.Recorder.Log);
    }

    [Fact]
    public async Task CallTool_CommandWithoutResult_ReturnsNullResult()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync("invoices_archive", new Dictionary<string, object?> { ["id"] = 7 }, cancellationToken: Ct);

        Assert.Equal(JsonValueKind.Null, result.StructuredContent!.Value.GetProperty("result").ValueKind);
        Assert.Equal(["archive 7"], mcp.Recorder.Log);
    }

    [Fact]
    public async Task CallTool_Query_ReturnsResultWithWebNaming()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync("invoices_get", new Dictionary<string, object?> { ["id"] = 3 }, cancellationToken: Ct);

        var invoice = result.StructuredContent!.Value.GetProperty("result");
        Assert.Equal(3, invoice.GetProperty("id").GetInt32());
        Assert.Equal("Ana", invoice.GetProperty("customer").GetString());
    }

    [Fact]
    public async Task CallTool_WithoutArguments_UsesDefaults()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);

        Assert.Equal(0, result.StructuredContent!.Value.GetProperty("result").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task CallTool_StreamQuery_ReturnsItems()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync("invoices_list", new Dictionary<string, object?> { ["count"] = 3 }, cancellationToken: Ct);

        var content = result.StructuredContent!.Value;
        Assert.Equal([1, 2, 3], content.GetProperty("items").EnumerateArray().Select(i => i.GetInt32()));
        Assert.False(content.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task CallTool_StreamQuery_IsCutShortAtTheLimit()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.MaxStreamResults = 2);

        var result = await mcp.Client.CallToolAsync("invoices_list", new Dictionary<string, object?> { ["count"] = 1000 }, cancellationToken: Ct);

        var content = result.StructuredContent!.Value;
        Assert.Equal([1, 2], content.GetProperty("items").EnumerateArray().Select(i => i.GetInt32()));
        Assert.True(content.GetProperty("truncated").GetBoolean());
        Assert.Equal(["stream disposed"], mcp.Recorder.Log);
    }

    [Fact]
    public async Task CallTool_InvalidArguments_ReportsSafeMessage()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync(
            "invoices_create",
            new Dictionary<string, object?> { ["customer"] = "Ana", ["amount"] = "secret-input" },
            cancellationToken: Ct);

        Assert.True(result.IsError);
        var text = Text(result);
        Assert.Equal("The argument 'amount' of action 'invoices.create' is not valid.", text);
        Assert.DoesNotContain("secret-input", text);
    }

    [Fact]
    public async Task CallTool_UnexpectedException_ReportsGenericErrorWithoutLeakingDetails()
    {
        await using var mcp = await McpHarness.StartAsync();

        var result = await mcp.Client.CallToolAsync("reports_fail", cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.DoesNotContain("secret internal detail", Text(result));
    }

    [Fact]
    public async Task CallTool_UnexpectedException_UsesErrorMessageWhenProvided()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.ErrorMessage = e => e is InvalidOperationException ? "The report couldn't be generated." : null);

        var result = await mcp.Client.CallToolAsync("reports_fail", cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Equal("The report couldn't be generated.", Text(result));
    }

    [Fact]
    public async Task CallTool_WithoutUser_ReportsAuthenticationRequired()
    {
        await using var mcp = await McpHarness.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(null)));

        var result = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Equal("Authentication is required.", Text(result));
    }

    [Fact]
    public async Task CallTool_ErrorMessage_WordsAuthorizationFailures_KeepingThemErrors()
    {
        await using var unauthenticated = await McpHarness.StartAsync(
            o => o.ErrorMessage = e => e is AuthenticationRequiredException ? "unauthenticated: sign in first" : null,
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(null)));
        await using var signedIn = await McpHarness.StartAsync(
            o => o.ErrorMessage = e => e is AccessDeniedException ? "forbidden" : null,
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn())));

        var missing = await unauthenticated.Client.CallToolAsync("invoices_get", cancellationToken: Ct);
        var denied = await signedIn.Client.CallToolAsync("invoices_archive", new Dictionary<string, object?> { ["id"] = 1 }, cancellationToken: Ct);

        Assert.True(missing.IsError);
        Assert.Equal("unauthenticated: sign in first", Text(missing));
        Assert.True(denied.IsError);
        Assert.Equal("forbidden", Text(denied));
        Assert.Empty(signedIn.Recorder.Log);
    }

    [Fact]
    public async Task CallTool_ErrorMessage_ReturningNull_KeepsTheAuthorizationTexts()
    {
        await using var mcp = await McpHarness.StartAsync(
            o => o.ErrorMessage = _ => null,
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(null)));

        var result = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Equal("Authentication is required.", Text(result));
    }

    [Fact]
    public async Task CallTool_ActionWithoutAuthorizer_ReportsAccessDenied()
    {
        await using var mcp = await McpHarness.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn()))
                .AddSingleton<IAuthorizer<GetInvoice>, AllowAll<GetInvoice>>());

        var denied = await mcp.Client.CallToolAsync("invoices_archive", new Dictionary<string, object?> { ["id"] = 1 }, cancellationToken: Ct);
        var allowed = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);

        Assert.True(denied.IsError);
        Assert.Equal("Access is denied.", Text(denied));
        Assert.Empty(mcp.Recorder.Log);
        Assert.NotEqual(true, allowed.IsError);
    }

    [Fact]
    public async Task CallTool_DenialWithDetails_ReportsAccessDenied_WithoutTheDetails()
    {
        await using var mcp = await McpHarness.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn()))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithDetails<ArchiveInvoice>>());

        var denied = await mcp.Client.CallToolAsync("invoices_archive", new Dictionary<string, object?> { ["id"] = 1 }, cancellationToken: Ct);

        Assert.True(denied.IsError);
        Assert.Equal("Access is denied.", Text(denied));
        Assert.DoesNotContain("TOP-SECRET", Text(denied));
    }

    [Fact]
    public async Task CallTool_DenialWithAnApplicationException_ReportsGenericError()
    {
        await using var mcp = await McpHarness.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn()))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithStepUp<ArchiveInvoice>>());

        var denied = await mcp.Client.CallToolAsync("invoices_archive", new Dictionary<string, object?> { ["id"] = 1 }, cancellationToken: Ct);

        Assert.True(denied.IsError);
        Assert.DoesNotContain("TOP-SECRET", Text(denied));
        Assert.Empty(mcp.Recorder.Log);
    }

    [Fact]
    public async Task CallTool_WithoutServiceProvider_ReportsGenericError()
    {
        await using var mcp = await McpHarness.StartAsync(withServiceProvider: false);

        var result = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);

        Assert.True(result.IsError);
    }

    [Fact]
    public void WithMediatorActions_InvalidArguments_Throw()
    {
        Assert.Equal("builder", Assert.Throws<ArgumentNullException>(() => ((IMcpServerBuilder)null!).WithMediatorActions()).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddMcpServer().WithMediatorActions(o => o.MaxStreamResults = 0));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMcpServer().WithMediatorActions(o =>
        {
            o.ErrorMessage = _ => "text";
            o.ErrorContent = _ => new { code = "x" };
        }));
    }

    [Fact]
    public async Task CallTool_ErrorContent_ReturnsStructuredContentAndItsJson()
    {
        await using var mcp = await McpHarness.StartAsync(o => o.ErrorContent = e => e switch
        {
            InvalidActionArgumentsException { Field: { } field } invalid => new
            {
                code = "invalid_arguments",
                errors = new Dictionary<string, string> { [field] = invalid.Reason is InvalidActionArgumentReason.Invalid ? "invalid" : "required" },
            },
            InvalidOperationException => new { code = "report_failed" },
            _ => null,
        });

        var invalid = await mcp.Client.CallToolAsync(
            "invoices_create",
            new Dictionary<string, object?> { ["customer"] = "Ana", ["amount"] = "secret-input" },
            cancellationToken: Ct);
        var failed = await mcp.Client.CallToolAsync("reports_fail", cancellationToken: Ct);

        Assert.True(invalid.IsError);
        Assert.Equal("""{"code":"invalid_arguments","errors":{"amount":"invalid"}}""", Text(invalid));
        Assert.Equal(Text(invalid), invalid.StructuredContent!.Value.GetRawText());
        Assert.True(failed.IsError);
        Assert.Equal("report_failed", failed.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallTool_ErrorContent_ReturningNull_KeepsMediatorsText()
    {
        await using var mcp = await McpHarness.StartAsync(
            o => o.ErrorContent = _ => null,
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(null)));

        var unauthenticated = await mcp.Client.CallToolAsync("invoices_get", cancellationToken: Ct);
        var failed = await mcp.Client.CallToolAsync("reports_fail", cancellationToken: Ct);

        Assert.True(unauthenticated.IsError);
        Assert.Equal("Authentication is required.", Text(unauthenticated));
        Assert.Null(unauthenticated.StructuredContent);
        Assert.True(failed.IsError);
        Assert.DoesNotContain("secret internal detail", Text(failed));
    }

    [Theory]
    [InlineData("text")]
    [InlineData(42)]
    public async Task CallTool_ErrorContent_NotAnObject_ReportsAGenericError(object content)
    {
        await using var mcp = await McpHarness.StartAsync(o => o.ErrorContent = _ => content);

        var result = await mcp.Client.CallToolAsync("reports_fail", cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        Assert.DoesNotContain("text", Text(result));
        Assert.DoesNotContain("42", Text(result));
        Assert.DoesNotContain("secret internal detail", Text(result));
    }

    [Fact]
    public void ActionWithInvalidToolName_FailsWhenTheServerIsConfigured()
    {
        var services = new ServiceCollection()
            .AddSingleton<IActionCatalog>(new SingleActionCatalog(new ActionDescriptor("Not Valid", "A test action.", ActionKind.Query, typeof(GetInvoice), typeof(InvoiceView))));
        services.AddMcpServer().WithMediatorActions();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<McpServerOptions>>().Value);
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    private sealed class SingleActionCatalog(ActionDescriptor action) : IActionCatalog
    {
        public IReadOnlyList<ActionDescriptor> Actions { get; } = [action];

        public bool TryGetAction(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ActionDescriptor? found)
        {
            found = name == action.Name ? action : null;
            return found is not null;
        }
    }
}
