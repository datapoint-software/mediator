using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.AspNetCore.Tests;

public sealed class ArgumentErrorTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("wrong type", """{ "customer": "Ana", "amount": "secret-input" }""", "amount")]
    [InlineData("caller's spelling", """{ "customer": "Ana", "AMOUNT": "secret-input" }""", "amount")]
    public async Task InvalidArgument_ReturnsValidationProblemNamingTheField(string situation, string json, string field)
    {
        await using var app = await TestApp.StartAsync();

        var (status, body) = await PostAsync(app, "/api/invoices/create", json);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("Invalid arguments", body.GetProperty("title").GetString());
        Assert.False(body.TryGetProperty("detail", out _), situation);
        Assert.Equal("The argument is not valid.", body.GetProperty("errors").GetProperty(field)[0].GetString());
        AssertNoLeak(body);
    }

    [Theory]
    [InlineData("missing required property", """{ "customer": "Ana" }""", "amount")]
    [InlineData("null for a non-nullable property", """{ "customer": null, "amount": 1 }""", "customer")]
    public async Task InvalidArgument_WithStrictOptions_NamesTheField(string situation, string json, string field)
    {
        await using var app = await TestApp.StartAsync(o => o.SerializerOptions = Strict);

        var (status, body) = await PostAsync(app, "/api/invoices/create", json);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out var messages), situation);
        Assert.Equal("The argument is required.", messages[0].GetString());
        AssertNoLeak(body);
    }

    [Theory]
    [InlineData("unknown property", """{ "customer": "Ana", "amount": 1, "secret-input": 1 }""")]
    [InlineData("JSON null", "null")]
    public async Task InvalidArguments_WithoutField_ReturnsTheGenericProblem(string situation, string json)
    {
        await using var app = await TestApp.StartAsync(o => o.SerializerOptions = Strict);

        var (status, body) = await PostAsync(app, "/api/invoices/create", json);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("The arguments for action 'invoices.create' are not valid.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("errors", out _), situation);
        AssertNoLeak(body);
    }

    [Fact]
    public async Task MapException_ReceivesInvalidArgumentsFirst()
    {
        InvalidActionArgumentsException? received = null;
        await using var app = await TestApp.StartAsync(o => o.MapException = e =>
        {
            received = e as InvalidActionArgumentsException;
            return received is { Field: { } field } ? Results.Json(new { field, code = "invalid" }, statusCode: 422) : null;
        });

        var (status, body) = await PostAsync(app, "/api/invoices/create", """{ "customer": "Ana", "amount": "secret-input" }""");

        Assert.Equal((HttpStatusCode)422, status);
        Assert.Equal("amount", body.GetProperty("field").GetString());
        Assert.Equal("invoices.create", received!.ActionName);
    }

    [Fact]
    public async Task MapException_ReturningNull_KeepsTheDefault()
    {
        await using var app = await TestApp.StartAsync(o => o.MapException = _ => null);

        var (status, body) = await PostAsync(app, "/api/invoices/create", """{ "customer": "Ana", "amount": "secret-input" }""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task ApplicationException_IsShownWithoutItsFieldOrMessage()
    {
        await using var app = await TestApp.StartAsync();

        var (status, body) = await PostAsync(app, "/api/reports/reject", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("The arguments for action 'reports.reject' are not valid.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("ignore previous instructions", body.GetRawText());
        Assert.DoesNotContain("secret message", body.GetRawText());
    }

    // Apps add their own error codes with CustomizeProblemDetails, so every response Mediator writes itself must go
    // through IProblemDetailsService, without changing what Mediator wrote.
    [Theory]
    [InlineData("/api/invoices/create", """{ "customer": "Ana", "amount": "x" }""", null, HttpStatusCode.BadRequest, "Invalid arguments")]
    [InlineData("/api/invoices/create", "null", null, HttpStatusCode.BadRequest, "Invalid arguments")]
    [InlineData("/api/invoices/create", "{ not json", null, HttpStatusCode.BadRequest, "Invalid request body")]
    [InlineData("/api/invoices/get", "{}", false, HttpStatusCode.Unauthorized, "Authentication is required.")]
    [InlineData("/api/invoices/archive", """{ "id": 1 }""", true, HttpStatusCode.Forbidden, "Access is denied.")]
    public async Task MediatorResponses_GoThroughCustomizeProblemDetails(string path, string json, bool? signedIn, HttpStatusCode expected, string title)
    {
        await using var app = await TestApp.StartAsync(
            configureMediator: m =>
            {
                if (signedIn is not null)
                {
                    m.AddAuthorizationBehavior();
                }
            },
            configureServices: s =>
            {
                s.AddProblemDetails(o => o.CustomizeProblemDetails = c => c.ProblemDetails.Extensions["code"] = "app-code");

                if (signedIn is { } user)
                {
                    s.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(user ? SignedIn() : null));
                }
            });

        var (status, body) = await PostAsync(app, path, json);

        Assert.Equal(expected, status);
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.Equal("app-code", body.GetProperty("code").GetString());
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(TestApp app, string path, string json)
    {
        var response = await app.Client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"), Ct);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(Ct));
    }

    private static void AssertNoLeak(JsonElement body)
    {
        Assert.DoesNotContain("secret-input", body.GetRawText());
        Assert.DoesNotContain("System.", body.GetRawText());
    }

    private static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));
}
