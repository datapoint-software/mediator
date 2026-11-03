# Actions

An action is a command or query with a name. Hosts can list actions and dispatch them by name, with JSON arguments. That's what lets one business layer become HTTP endpoints, CLI commands and MCP tools without writing each one by hand.

```csharp
[Api]
[CommandLine]
[ModelContextProtocol]
[Action("invoices.create", "Creates an open invoice for a customer and returns its id.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;
```

| Host | Registration | `invoices.create` becomes |
|---|---|---|
| ASP.NET Core | `app.MapMediatorActions("/api")` | `POST /api/invoices/create` |
| Command line | `root.AddMediatorActions(services)` | `mytool invoices create --customer Ana --amount 100` |
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

Every action needs a description, the second argument of `[Action]`. It becomes the OpenAPI summary, the CLI command's help, and the text an AI agent reads to choose a tool. Write it for someone who has never seen your code: what the action does, and what it returns. An empty or whitespace description throws when `AddMediator` runs.

For CLI help and MCP schemas, describe each argument with `[Description]` from `System.ComponentModel`:

```csharp
public sealed record CreateInvoice(
    [property: Description("The customer's name.")] string Customer,
    [property: Description("The amount to invoice, greater than zero.")] decimal Amount) : ICommand<Guid>;
```

## Where an action is offered

Mark each action with the hosts that offer it. A host offers only the actions marked for it:

| Attribute | Host |
|---|---|
| `[Api]` | ASP.NET Core, `MapMediatorActions` |
| `[CommandLine]` | Command line, `AddMediatorActions` |
| `[ModelContextProtocol]` | MCP server, `WithMediatorActions` |

```csharp
[Api]
[Action("users.delete", "Deletes a user, given their identifier.")]
public sealed record DeleteUser(Guid Id) : ICommand;   // an endpoint, but not a CLI command or an MCP tool
```

- An action a host doesn't offer fails there like an unknown one, so callers can't discover it by guessing names.
- An action without any of these attributes throws when `AddMediator` runs, with every such action listed. Forgetting a host hides the action from it; forgetting them all is caught at startup.
- Each host's `Filter` still applies, after the attribute, so it can only narrow what the host offers, for example behind a feature flag.
- Code that only your application runs, such as a background job's command, isn't an action. Leave out `[Action]`, and send it through `IMediator`.
- A CLI that sends its actions to your API, instead of running them in-process, needs them marked `[CommandLine]` for itself and `[Api]` for the server.

### Custom hosts

A host you write yourself defines its own attribute, and offers only the actions marked with it:

```csharp
public sealed class GrpcAttribute : ActionChannelAttribute;

foreach (var action in catalog.Actions.Where(action => action.IsOfferedBy<GrpcAttribute>()))
{
    // Offer the action.
}
```

`IsOfferedBy` matches that exact attribute. `IActionDispatcher` dispatches any action in the catalog, so a custom host must never dispatch a name it doesn't offer: look the action up in its own list first.

## Host-provided properties

Some properties are filled in by the host, never by the caller, such as the tenant of the caller's session. Mark them `[HostProvided]`, on the property or on the record parameter:

```csharp
[Api]
[ModelContextProtocol]
[Action("invoices.list", "Lists the invoices of the signed-in user's tenant.")]
public sealed record ListInvoices(
    [HostProvided(Reason = "The tenant of the caller's session.")] Guid TenantId,
    string? Customer) : IQuery<IReadOnlyList<InvoiceSummary>>;
```

| Host | The property | A value from the caller |
|---|---|---|
| Command line | has no option, and is never required | in `--json`: refused, exit code 2 |
| MCP server | isn't in the tool's input schema | refused, as a tool error |
| ASP.NET Core | is `readOnly` in OpenAPI, with [a schema transformer](hosts/aspnetcore.md#host-provided-properties) | accepted, so the host must overwrite it |
| `IActionDispatcher`, `IMediator` | is an ordinary property | accepted, so the host must set it |

The refusal reads `'tenantId' is set by the server and cannot be given.` It matches the property's JSON name ignoring case, as System.Text.Json does when binding, so `"TenantId"` is refused too. Only the action's own properties count, not properties of nested objects.

> **Hiding is presentation only.** The host must set or overwrite the value before the action is dispatched, and must never trust a value that came from the caller. A host that forgets dispatches the default value, such as `Guid.Empty`, so make your handlers or authorizers refuse a default tenant.

### Filling the value

An API can overwrite the property in middleware, before `MapMediatorActions` reads the body. Hosts that dispatch in-process, such as an MCP server or a CLI that runs the actions itself, set it in a decorator of `IActionDispatcher`. The adapters refuse the caller's value **before** they call the dispatcher, so the decorator's value never trips the refusal:

1. The adapter refuses arguments that contain a host-provided property.
2. It calls `IActionDispatcher`, which is your decorator.
3. The decorator sets the value, replacing any value in the arguments.
4. The inner `ActionDispatcher` deserializes the action and sends it.

```csharp
// Sets every action's TenantId from the caller's session, after the host has refused any tenant from the caller.
public sealed class TenantDispatcher(ActionDispatcher inner, IActionCatalog catalog, IPrincipalAccessor principals)
    : IActionDispatcher
{
    public ValueTask<object?> DispatchAsync(
        string name, JsonElement arguments, JsonSerializerOptions? options = null, CancellationToken ct = default) =>
        inner.DispatchAsync(name, WithTenant(name, arguments, options), options, ct);

    public IAsyncEnumerable<object?> StreamAsync(
        string name, JsonElement arguments, JsonSerializerOptions? options = null, CancellationToken ct = default) =>
        inner.StreamAsync(name, WithTenant(name, arguments, options), options, ct);

    private JsonElement WithTenant(string name, JsonElement arguments, JsonSerializerOptions? options)
    {
        if (!catalog.TryGetAction(name, out var action)
            || !action.HostProvidedProperties.Any(p => p.Name == "TenantId")
            || arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
        {
            return arguments;   // unknown actions and invalid arguments fail in the inner dispatcher, as usual
        }

        var json = arguments.ValueKind == JsonValueKind.Object ? JsonObject.Create(arguments)! : new JsonObject();
        var key = (options ?? JsonSerializerOptions.Web).PropertyNamingPolicy?.ConvertName("TenantId") ?? "TenantId";

        // Replace, never add only if missing: remove the caller's value in any letter case.
        foreach (var existing in json.Select(p => p.Key).Where(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            json.Remove(existing);
        }

        if (principals.Principal?.FindFirstValue("tenant_id") is { } tenantId)
        {
            json[key] = tenantId;   // otherwise the default, which the authorizer refuses
        }

        return JsonSerializer.SerializeToElement(json);
    }
}
```

`AddMediator` registers `ActionDispatcher` only if no `IActionDispatcher` is registered, so register the decorator after it:

```csharp
services.AddMediator(...);
services.AddScoped<ActionDispatcher>();
services.Replace(ServiceDescriptor.Scoped<IActionDispatcher, TenantDispatcher>());
```

Don't write host-provided values into the caller's arguments before the adapter sees them, for example in MCP middleware that edits `tools/call` arguments: the adapter would refuse every call.

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
- Arguments that aren't valid JSON for the action's type throw `InvalidActionArgumentsException`. Its `Field` names the argument that failed, as it's spelled on the wire, such as `count` or `lines[1].quantity`, its `Reason` says why, and its message never repeats the input. See [Invalid arguments](#invalid-arguments).
- Pass `JsonSerializerOptions` to control naming and converters. Hosts use their own: see each host's guide.

## Invalid arguments

`InvalidActionArgumentsException` says which argument failed and why, never its value:

| Arguments | `Field` | `Reason` |
|---|---|---|
| `{ "count": "abc" }` for an `int` | `count` | `Invalid` |
| `{ "count": null }` for an `int` | `count` | `Null` |
| `{ "lines": [ {}, { "quantity": "abc" } ] }` | `lines[1].quantity` | `Invalid` |
| `{ "COUNT": "abc" }`, bound ignoring case | `count`, the contract's spelling, not the caller's | `Invalid` |
| `{ "tags": { "any key": "abc" } }` for a dictionary | `tags`: it stops before the key, which the caller chose | `Invalid` |
| a missing required property, with strict options | the missing property, such as `id` | `Missing` |
| `null` for a non-nullable property, with strict options | the property, such as `name` | `Null` |
| an unknown property, with strict options | `null`: the caller chose its name | `null` |
| malformed JSON, or JSON `null` | `null` | `null` |

`Reason` is `null` whenever `Field` is. Report `Missing` and `Null` as a required value, such as "Enter a hostname", and `Invalid` as a value of the wrong type or format.

`ActionName` is the action's name. The message, for logs, says the same: `The argument 'count' of action 'orders.create' is not valid.`, or `... is required.` for `Missing` and `Null`.

### Strict arguments

With the default options, a missing `int` becomes `0`, and `null` reaches a non-nullable `string`. To refuse them, use options based on `JsonSerializerOptions.Strict`, with camelCase names, in every host:

```csharp
var arguments = new JsonSerializerOptions(JsonSerializerOptions.Strict)
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};
```

They refuse unknown properties, duplicate properties and `null` for non-nullable properties, and they require constructor parameters without a default value. Use positional records, or mark properties `required`, so a missing argument is refused too. Strict options match names case-sensitively, so callers must send `count`, not `Count`.

### Checking arguments before sending them

`ActionArguments.Deserialize` reads arguments the way the dispatcher does, so a host that sends them elsewhere, such as a CLI calling a remote server, can refuse them locally with the same `Field`:

```csharp
if (!catalog.TryGetAction(name, out var action) || !action.IsOfferedBy<CommandLineAttribute>())
{
    throw new ActionNotFoundException();
}

// Throws InvalidActionArgumentsException, like the dispatcher.
var instance = ActionArguments.Deserialize(action, arguments, options);
```

An undefined `JsonElement` means no arguments (`{}`), and JSON `null` throws. It never returns `null`.

## Writing your own host

When building a host on `IActionDispatcher`, follow the same rules as the built-in ones:
- Map `InvalidActionArgumentsException`, `ActionNotFoundException`, `AuthenticationRequiredException` and `AccessDeniedException` to fixed, safe responses. Describe invalid arguments from `Field` and `Reason`, never from the exception's message.
- Never return or print any other exception's message, or the caller's input.
- Don't offer the properties in `ActionDescriptor.HostProvidedProperties` to callers, and refuse a caller's value for one, matching its JSON name with `StringComparer.OrdinalIgnoreCase`.
- Create a DI scope per call, and pass the caller's cancellation token.
