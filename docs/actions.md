# Actions

An action is a command or query with a name. Hosts can list actions and dispatch them by name, with JSON arguments. That's what lets one business layer become HTTP endpoints, CLI commands and MCP tools without writing each one by hand.

```csharp
[Action("invoices.create", Description = "Creates an open invoice for a customer and returns its id.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;
```

| Host | Registration | `invoices.create` becomes |
|---|---|---|
| ASP.NET Core | `app.MapActions("/api")` | `POST /api/invoices/create` |
| Command line | `root.AddActions(services)` | `mytool invoices create --customer Ana --amount 100` |
| MCP server | `.WithMediatorActions()` | the tool `invoices_create` |

Every behaviour still runs, so an action dispatched by name is authorized, validated and wrapped in a transaction like any other.

## What can be an action

Commands (with or without a result), queries and stream queries, when they have a registered handler. Messages can't: they aren't authorized, so they must never be reachable from a host. Marking one throws at startup.

## Names

- Lowercase letters and digits, in segments separated by dots or hyphens: `^[a-z0-9]+([.-][a-z0-9]+)*$`.
- Up to 64 characters.
- Unique across the application.

Use dots to group actions, such as `invoices.create` and `invoices.lines.add`: hosts turn them into URL segments, subcommands or tool name prefixes. An invalid or duplicate name throws when `AddMediator` runs.

> An action's name is part of your public contract, like a URL. Renaming the class doesn't change it, but changing the name breaks every caller.

## Descriptions

`Description` becomes the OpenAPI summary, the CLI command's help, and the text an AI agent reads to choose a tool. Write it for someone who has never seen your code: what the action does, and what it returns.

For CLI help and MCP schemas, describe each argument with `[Description]` from `System.ComponentModel`:

```csharp
public sealed record CreateInvoice(
    [property: Description("The customer's name.")] string Customer,
    [property: Description("The amount to invoice, greater than zero.")] decimal Amount) : ICommand<Guid>;
```

## Hidden actions

`Hidden = true` keeps an action out of the catalog and out of dispatch by name, while your own code can still send it through `IMediator`:

```csharp
[Action("invoices.purge", Hidden = true)]
public sealed record PurgeCancelledInvoices : ICommand;
```

An unknown action and a hidden one fail the same way, with `ActionNotFoundException` and the same message, so callers can't discover internal actions by guessing names.

## The catalog and the dispatcher

Hosts use two services. You only need them to build your own host:

```csharp
// Lists the visible actions: name, description, kind, action type and result type.
foreach (var action in catalog.Actions)
{
    Console.WriteLine($"{action.Name}: {action.Description}");
}

// Dispatches by name, with the arguments as JSON.
using var arguments = JsonDocument.Parse("""{ "customer": "Ana", "amount": 100 }""");
var result = await dispatcher.DispatchAsync("invoices.create", arguments.RootElement, ct: ct);

// Stream queries
await foreach (var item in dispatcher.StreamAsync("invoices.list", default, ct: ct)) { }
```

- `IActionCatalog` is a singleton, and `IActionDispatcher` is scoped.
- `DispatchAsync` returns the result as `object?`, or `null` for a command without a result.
- An undefined `JsonElement` (`default`) means no arguments.
- Arguments that aren't valid JSON for the action's type throw `InvalidActionArgumentsException`, whose message never repeats the input.
- Pass `JsonSerializerOptions` to control naming and converters. Hosts use their own: see each host's guide.

## Writing your own host

When building a host on `IActionDispatcher`, follow the same rules as the built-in ones:
- Map `InvalidActionArgumentsException`, `ActionNotFoundException`, `AuthenticationRequiredException` and `AccessDeniedException` to fixed, safe responses.
- Never return or print any other exception's message, or the caller's input.
- Create a DI scope per call, and pass the caller's cancellation token.
