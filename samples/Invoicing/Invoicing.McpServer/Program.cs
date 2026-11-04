using Invoicing.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Over stdio, standard output carries the protocol, so every log goes to standard error.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddInvoicing();

// The server runs on the user's machine and acts for them: INVOICING_USER and INVOICING_ROLES. An agent never gets more
// permissions than this user, so consider a narrower principal, for example without the Finance role.
builder.Services.AddFixedPrincipalAccessor(InvoicingServiceCollectionExtensions.PrincipalFromEnvironment());

// One tool per action: invoices_create, invoices_get, invoices_list and invoices_cancel.
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    // Anything InvoicingErrors doesn't describe is reported as a generic error that reveals nothing.
    .WithMediatorActions(o => o.ErrorMessage = InvoicingErrors.Describe);

var app = builder.Build();
await app.Services.EnsureInvoicingDatabaseAsync();
await app.RunAsync();
