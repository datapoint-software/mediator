using FluentValidation;
using Invoicing.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInvoicing();

// The request's user comes from a JWT bearer token. In development, create one with `dotnet user-jwts`.
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddHttpPrincipalAccessor();   // commands and queries run as the request's user
builder.Services.AddOpenApi();

var app = builder.Build();
await app.Services.EnsureInvoicingDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();   // GET /openapi/v1.json describes every action

// One POST endpoint per action: /api/invoices/create, /api/invoices/get, /api/invoices/list, /api/invoices/cancel.
app.MapMediatorActions("/api", o => o.MapException = e => e switch
    {
        // Property names and messages only: never the attempted values.
        ValidationException v => Results.ValidationProblem(v.Errors
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray())),
        InvoiceNotFoundException => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The invoice doesn't exist."),
        _ => null,   // anything else goes to ASP.NET Core's exception handling, without details
    })
    .RequireAuthorization();   // ASP.NET Core refuses anonymous requests; the mediator then checks each action's rules

app.Run();
