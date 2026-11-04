using System.CommandLine;
using Invoicing.Domain;
using Microsoft.Extensions.DependencyInjection;

// The CLI acts for one user for the whole run: INVOICING_USER and INVOICING_ROLES, or nobody when they aren't set.
await using var services = new ServiceCollection()
    .AddInvoicing()
    .AddFixedPrincipalAccessor(InvoicingServiceCollectionExtensions.PrincipalFromEnvironment())
    .BuildServiceProvider();

await services.EnsureInvoicingDatabaseAsync();

// One command per action: `invoicing invoices create --customer Ana --amount 100`. Results are printed as JSON.
var root = new RootCommand("Invoicing sample: the same actions as the API and the MCP server.")
    // Anything InvoicingErrors doesn't describe is reported as "An unexpected error occurred.", with exit code 70.
    .AddMediatorActions(services, o => o.ErrorMessage = InvoicingErrors.Describe);

return await root.Parse(args).InvokeAsync();
