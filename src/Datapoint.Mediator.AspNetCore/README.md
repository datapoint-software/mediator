# Datapoint.Mediator.AspNetCore

Maps [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as ASP.NET Core endpoints, with OpenAPI metadata and safe `ProblemDetails` errors, so an API offers the same commands and queries as a CLI or an MCP server.

```csharp
[Action("invoices.create", "Creates an invoice for a customer.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

app.MapMediatorActions("/api").RequireAuthorization();   // POST /api/invoices/create
```

Invalid arguments return 400, missing authentication 401 and denied access 403, never repeating the request's input. Map your own exceptions, such as validation failures, with `options.MapException`.

**Documentation:** [ASP.NET Core](https://github.com/datapoint-software/mediator/blob/main/docs/hosts/aspnetcore.md) · [Actions](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md)
