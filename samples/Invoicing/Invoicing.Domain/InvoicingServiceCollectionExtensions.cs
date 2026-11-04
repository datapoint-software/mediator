using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Invoicing.Domain;

public static class InvoicingServiceCollectionExtensions
{
    // Every host calls this, so all three share the same handlers, rules and behaviours.
    public static IServiceCollection AddInvoicing(this IServiceCollection services)
    {
        services.AddDbContext<InvoicingDbContext>(o => o.UseSqlite($"Data Source={DatabasePath}"));
        services.AddValidatorsFromAssemblyContaining<CreateInvoiceValidator>();
        services.AddAuthorizersFromAssemblyContaining<InvoiceAuthorizer>();
        services.AddMediator(o => o
            .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
            .AddAuthorizationBehavior()                       // first: refuse before anything else runs
            .AddValidationBehavior()                          // then: refuse invalid arguments
            .AddTransactionBehavior<InvoicingDbContext>());   // last: only allowed, valid commands open a transaction
        return services;
    }

    // One SQLite file shared by every host, so an invoice created from the CLI is visible from the API. It's in the
    // user's own application data folder, not the shared temporary folder, where other users could read or replace it.
    public static string DatabasePath =>
        Environment.GetEnvironmentVariable("INVOICING_DATABASE")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            "datapoint-mediator-invoicing.db");

    public static async Task EnsureInvoicingDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InvoicingDbContext>().Database.EnsureCreatedAsync(ct);
    }

    // For hosts that act for one user, such as the CLI and the MCP server: INVOICING_USER names the user, and
    // INVOICING_ROLES lists their roles, separated by commas. With no user, actions fail with "authentication required".
    //
    // Security: whoever sets these variables chooses the user and the roles. That's only safe when the process runs on
    // the user's own machine, with access to nothing they couldn't reach anyway. A CLI or MCP server shared by several
    // users, or hosted for others, must sign them in instead, for example with OAuth, and read the roles from the token.
    public static ClaimsPrincipal? PrincipalFromEnvironment()
    {
        var name = Environment.GetEnvironmentVariable("INVOICING_USER");

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var roles = (Environment.GetEnvironmentVariable("INVOICING_ROLES") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Claim[] claims = [new(ClaimTypes.Name, name), .. roles.Select(r => new Claim(ClaimTypes.Role, r))];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "environment"));
    }

    // Validation failures as "Property: message" lines, without the attempted values, which could be anything the
    // caller sent and must never be echoed back.
    public static string Describe(ValidationException exception) =>
        string.Join("; ", exception.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
}
