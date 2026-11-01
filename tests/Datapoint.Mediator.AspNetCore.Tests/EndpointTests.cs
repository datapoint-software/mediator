using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.AspNetCore.Tests;

public sealed class EndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CommandWithResult_Returns200WithResult()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/api/invoices/create", new { customer = "Ana", amount = 100 }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CreateInvoiceHandler.Id, await response.Content.ReadFromJsonAsync<Guid>(Ct));
    }

    [Fact]
    public async Task CommandWithoutResult_Returns204()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/api/invoices/archive", new { id = 7 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["archive 7"], app.App.Services.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task Query_Returns200WithResult()
    {
        await using var app = await TestApp.StartAsync();

        var json = await PostAsync(app, "/api/invoices/get", """{ "id": 3 }""");

        Assert.Equal(3, json.GetProperty("id").GetInt32());
        Assert.Equal("Ana", json.GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task EmptyBody_IsTreatedAsEmptyObject()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsync("/api/invoices/get", null, Ct);

        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task StreamQuery_ReturnsJsonArray()
    {
        await using var app = await TestApp.StartAsync();

        var json = await PostAsync(app, "/api/invoices/list", """{ "count": 3 }""");

        Assert.Equal([1, 2, 3], json.EnumerateArray().Select(i => i.GetInt32()));
    }

    [Fact]
    public async Task EmptyStreamQuery_ReturnsEmptyArray()
    {
        await using var app = await TestApp.StartAsync();

        var json = await PostAsync(app, "/api/invoices/list", """{ "count": 0 }""");

        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public async Task AppJsonOptions_AreUsed()
    {
        await using var app = await TestApp.StartAsync(configureServices: s =>
            s.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));

        var json = await PostAsync(app, "/api/invoices/get", """{ "id": 3 }""");

        Assert.Equal("Ana", json.GetProperty("customer_name").GetString());
    }

    [Fact]
    public async Task MalformedBody_Returns400WithoutEchoingInput()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsync("/api/invoices/get", new StringContent("{ secret-input", Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("secret-input", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task InvalidArguments_Returns400WithoutEchoingInput()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsync(
            "/api/invoices/create",
            new StringContent("""{ "customer": "Ana", "amount": "secret-input" }""", Encoding.UTF8, "application/json"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("are not valid", body);
        Assert.DoesNotContain("secret-input", body);
    }

    [Fact]
    public async Task NoUser_Returns401()
    {
        await using var app = await StartWithAuthorizationAsync(principal: null);

        var response = await app.Client.PostAsync("/api/invoices/get", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActionWithoutAuthorizer_Returns403_AndAllowedActionSucceeds()
    {
        await using var app = await StartWithAuthorizationAsync(SignedIn());

        var denied = await app.Client.PostAsJsonAsync("/api/invoices/archive", new { id = 1 }, Ct);
        var allowed = await app.Client.PostAsync("/api/invoices/get", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(app.App.Services.GetRequiredService<Recorder>().Log);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task DenialWithDetails_Returns403_WithoutTheDetails()
    {
        await using var app = await TestApp.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn()))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithDetails<ArchiveInvoice>>());

        var response = await app.Client.PostAsJsonAsync("/api/invoices/archive", new { id = 1 }, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("42", body);
        Assert.DoesNotContain("TOP-SECRET", body);
    }

    [Fact]
    public async Task DenialWithAnApplicationException_GoesThroughMapException()
    {
        await using var app = await TestApp.StartAsync(
            o => o.MapException = e => e is StepUpRequiredException ? Results.StatusCode(428) : null,
            configureMediator: m => m.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(SignedIn()))
                .AddSingleton<IAuthorizer<ArchiveInvoice>, DenyWithStepUp<ArchiveInvoice>>());

        var response = await app.Client.PostAsJsonAsync("/api/invoices/archive", new { id = 1 }, Ct);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.DoesNotContain("TOP-SECRET", await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(app.App.Services.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task DeniedStreamQuery_Returns403_NotABroken200()
    {
        await using var app = await StartWithAuthorizationAsync(SignedIn());

        var response = await app.Client.PostAsJsonAsync("/api/invoices/list", new { count = 3 }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_UsesMapExceptionWhenItHandlesIt()
    {
        await using var app = await TestApp.StartAsync(o => o.MapException = e => e is InvalidOperationException ? Results.StatusCode(422) : null);

        var response = await app.Client.PostAsync("/api/reports/fail", null, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_IsLeftToTheAppsExceptionHandling()
    {
        await using var app = await TestApp.StartAsync(o => o.MapException = _ => null);

        // The test server passes unhandled exceptions to the client, so they surface here unchanged.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Client.PostAsync("/api/reports/fail", null, Ct));

        Assert.Equal("secret internal detail", exception.Message);
    }

    [Fact]
    public async Task Filter_LimitsMappedActions()
    {
        await using var app = await TestApp.StartAsync(o => o.Filter = a => a.Kind == ActionKind.Query);

        Assert.Equal(["/api/invoices/get"], RouteEndpoints(app).Select(e => e.RoutePattern.RawText));
    }

    [Fact]
    public async Task Endpoints_CarryOpenApiMetadataAndGroupConventions()
    {
        await using var app = await TestApp.StartAsync(
            configureServices: s => s.AddAuthorization(),
            configureGroup: g => g.RequireAuthorization());

        var endpoints = RouteEndpoints(app).ToDictionary(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName);

        Assert.Equal(["invoices.archive", "invoices.create", "invoices.get", "invoices.list", "reports.fail"], endpoints.Keys.Order(StringComparer.Ordinal));
        var create = endpoints["invoices.create"];
        Assert.Equal("/api/invoices/create", create.RoutePattern.RawText);
        Assert.Equal(typeof(CreateInvoice), create.Metadata.GetMetadata<IAcceptsMetadata>()!.RequestType);
        Assert.Equal("Creates an invoice for a customer.", create.Metadata.GetMetadata<IEndpointSummaryMetadata>()!.Summary);
        Assert.Contains("invoices", create.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Contains(create.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(), p => p.StatusCode == 200 && p.Type == typeof(Guid));
        Assert.Contains(create.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(), p => p.StatusCode == 403);
        Assert.Contains(endpoints["invoices.archive"].Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(), p => p.StatusCode == 204);
        Assert.Contains(endpoints["invoices.list"].Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(), p => p.StatusCode == 200 && p.Type == typeof(System.Collections.Generic.IEnumerable<int>));
        Assert.All(endpoints.Values, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task MapActions_NullArguments_Throw()
    {
        await using var app = await TestApp.StartAsync();

        Assert.Equal("endpoints", Assert.Throws<ArgumentNullException>(() => ((IEndpointRouteBuilder)null!).MapActions()).ParamName);
        Assert.Equal("prefix", Assert.Throws<ArgumentNullException>(() => app.App.MapActions(null!)).ParamName);
    }

    private static Task<TestApp> StartWithAuthorizationAsync(ClaimsPrincipal? principal) =>
        TestApp.StartAsync(
            configureMediator: o => o.AddAuthorizationBehavior(),
            configureServices: s => s
                .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal))
                .AddSingleton<IAuthorizer<GetInvoice>, AllowAll<GetInvoice>>());

    private static async Task<JsonElement> PostAsync(TestApp app, string path, string json)
    {
        var response = await app.Client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static RouteEndpoint[] RouteEndpoints(TestApp app) =>
        ((IEndpointRouteBuilder)app.App).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToArray();

    private static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));
}
