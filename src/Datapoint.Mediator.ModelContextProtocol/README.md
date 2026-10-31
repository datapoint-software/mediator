# Datapoint.Mediator.ModelContextProtocol

Offers [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as [Model Context Protocol](https://modelcontextprotocol.io) (MCP) tools, so AI agents can call your commands and queries, with authorization, validation, transactions and telemetry applied as for any other caller.

```csharp
[Action("invoices.create", Description = "Creates an invoice for a customer and returns its id.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

builder.Services.AddMediator(o => o.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>());
builder.Services.AddMcpServer()
    .WithStdioServerTransport()          // or .WithHttpTransport(), with app.MapMcp()
    .WithMediatorActions();
```

- Every action in the catalog becomes a tool. Its name has dots replaced by underscores (`invoices_create`), because many AI model APIs don't accept dots in tool names.
- The tool's description comes from `[Action(Description = ...)]`, and its input schema from the action's type. Write descriptions for the AI agent: they're how it decides which tool to use.
- Queries are marked read-only, and commands as possibly destructive, so MCP clients can ask the user before running them.
- A command or query returns `{ "result": ... }`. A stream query returns `{ "items": [...], "truncated": false }`, cut short at `MaxStreamResults` (100 by default).
- `options.Filter` limits which actions are offered, for example `o.Filter = a => a.Name.StartsWith("invoices.")`.

## Errors

Authorization failures and invalid arguments are reported with fixed, safe messages. Any other exception is reported as a generic error that reveals nothing about it, unless `options.ErrorMessage` returns a safe message for it:

```csharp
.WithMediatorActions(o => o.ErrorMessage = e => e is ValidationException v
    ? string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")) // never AttemptedValue
    : null)
```

## The current user

Register an `IPrincipalAccessor` (from Datapoint.Mediator.Authorization) for the MCP host:

- **HTTP:** return `IHttpContextAccessor.HttpContext?.User`, after authenticating the MCP endpoint, for example with `app.MapMcp().RequireAuthorization()`.
- **stdio:** the server runs on the user's machine, so return a principal for the local user, or for a service account.

> **Security:** an AI agent acts on behalf of a user, so it must never receive more permissions than that user. Consider giving agents a narrower principal, for example read-only, and remember that every tool you offer can be called with any arguments the agent chooses.
