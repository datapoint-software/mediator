using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Xunit;

namespace Datapoint.Mediator.AspNetCore.Tests;

public sealed class HostProvidedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Body_WithTheProperty_StillBinds()
    {
        await using var app = await TestApp.StartAsync(configureMediator: o => o.AddHandler<TenantTotalsHandler>());
        var tenantId = Guid.NewGuid();

        var response = await app.Client.PostAsJsonAsync("/api/reports/totals", new { tenantId, customer = "Ana" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{tenantId} Ana", await response.Content.ReadFromJsonAsync<string>(Ct));
    }

    [Fact]
    public async Task OpenApi_WithTheDocumentedTransformer_MarksThePropertyReadOnly()
    {
        await using var app = await TestApp.StartAsync(
            configureMediator: o => o.AddHandler<TenantTotalsHandler>(),
            configureServices: s => s.AddOpenApi(o => o.AddSchemaTransformer(MarkHostProvidedReadOnly)),
            configureApp: a => a.MapOpenApi());

        using var document = JsonDocument.Parse(await app.Client.GetStringAsync("/openapi/v1.json", Ct));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var totals = schemas.GetProperty(nameof(TenantTotals));
        var invoice = schemas.GetProperty(nameof(CreateInvoice));

        Assert.True(totals.GetProperty("properties").GetProperty("tenantId").GetProperty("readOnly").GetBoolean());
        Assert.False(totals.GetProperty("properties").GetProperty("customer").TryGetProperty("readOnly", out _));
        Assert.DoesNotContain("tenantId", Required(totals));
        Assert.Contains("customer", Required(totals));
        Assert.Equal(["amount", "customer"], Required(invoice).Order(StringComparer.Ordinal));
    }

    private static string?[] Required(JsonElement schema) =>
        schema.TryGetProperty("required", out var required) ? [.. required.EnumerateArray().Select(r => r.GetString())] : [];

    // The transformer shown in docs/hosts/aspnetcore.md, kept identical.
    private static Task MarkHostProvidedReadOnly(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var catalog = context.ApplicationServices.GetRequiredService<IActionCatalog>();
        var action = catalog.Actions.FirstOrDefault(a => a.ActionType == context.JsonTypeInfo.Type);

        foreach (var property in action is null ? [] : context.JsonTypeInfo.Properties)
        {
            if (property.AttributeProvider is PropertyInfo member
                && action!.HostProvidedProperties.Any(p => p.Name == member.Name)
                && schema.Properties is { } properties
                && properties.TryGetValue(property.Name, out var propertySchema)
                && propertySchema is OpenApiSchema inline)
            {
                inline.ReadOnly = true;
                schema.Required?.Remove(property.Name);
            }
        }

        return Task.CompletedTask;
    }
}

[Api]
[Action("reports.totals", "A test action.")]
public sealed record TenantTotals([HostProvided] Guid TenantId, string Customer) : IQuery<string>;

public sealed class TenantTotalsHandler : IQueryHandler<TenantTotals, string>
{
    public ValueTask<string> HandleAsync(TenantTotals query, CancellationToken ct = default) => new($"{query.TenantId} {query.Customer}");
}
