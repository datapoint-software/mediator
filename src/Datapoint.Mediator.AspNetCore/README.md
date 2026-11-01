# Datapoint.Mediator.AspNetCore

Maps [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as ASP.NET Core endpoints, so an API offers the same commands and queries as a CLI or an MCP server, with authorization, validation, transactions and telemetry applied.

```csharp
[Action("invoices.create", Description = "Creates an invoice for a customer.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

builder.Services.AddMediator(o => o.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>());
var app = builder.Build();
app.MapActions("/api").RequireAuthorization();   // POST /api/invoices/create
```

- Each action becomes a `POST` endpoint at its name with dots replaced by slashes. The request body is the action's JSON, and an empty body means no arguments.
- A command without a result returns `204`; other actions return `200` with their result as JSON. Stream queries stream a JSON array as results arrive.
- `MapActions` returns a `RouteGroupBuilder`, so conventions such as `RequireAuthorization`, rate limiting or CORS apply to every action.
- Each endpoint describes its request and response types, name, summary and tag, so .NET's OpenAPI support (`builder.Services.AddOpenApi()`) documents every action.
- JSON uses the app's `JsonOptions`, unless `options.SerializerOptions` is set. `options.Filter` limits which actions are mapped.

## Errors

Invalid JSON or arguments return `400`, missing authentication `401`, and denied access `403`, as `ProblemDetails` that never repeat the request's input. Any other exception goes to `options.MapException`, and is otherwise left to the app's exception handling, such as `UseExceptionHandler`:

```csharp
app.MapActions("/api", o => o.MapException = e => e is ValidationException v
    ? Results.ValidationProblem(v.Errors.GroupBy(f => f.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()))   // never AttemptedValue
    : null);
```

## The current user

To authorize actions for the request's user, add [Datapoint.Mediator.Authorization.AspNetCore](https://www.nuget.org/packages/Datapoint.Mediator.Authorization.AspNetCore):

```csharp
builder.Services.AddHttpPrincipalAccessor();
// ...
app.UseAuthentication();
```
