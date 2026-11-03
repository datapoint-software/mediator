# Model Context Protocol

`Datapoint.Mediator.ModelContextProtocol` offers every [action](../actions.md) as a [Model Context Protocol](https://modelcontextprotocol.io) (MCP) tool, so AI agents, such as Claude, can call your commands and queries, with the same authorization and validation as any other caller.

```bash
dotnet add package Datapoint.Mediator.ModelContextProtocol --prerelease
```

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);   // stdout is the protocol

builder.Services.AddInvoicing();                                         // your AddMediator(...) call
builder.Services.AddFixedPrincipalAccessor(principal);                   // who the agent acts for
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithMediatorActions();

await builder.Build().RunAsync();
```

The full server is in [`samples/Invoicing/Invoicing.McpServer`](../../samples/Invoicing/Invoicing.McpServer/Program.cs).

## Transports

| Transport | Use it when | The current user |
|---|---|---|
| stdio: `.WithStdioServerTransport()` | the server runs on the user's machine, started by their MCP client | `AddFixedPrincipalAccessor(principal)` |
| HTTP: `.WithHttpTransport()` and `app.MapMcp()` | the server runs remotely, for many users | `AddHttpPrincipalAccessor()`, with `app.MapMcp().RequireAuthorization()` |

Over stdio, standard output carries the protocol, so send all logs to standard error, as in the example above.

## Tools

- Each action marked `[ModelContextProtocol]` becomes a tool, named with dots replaced by underscores, such as `invoices_create`, because many AI model APIs don't accept dots in tool names. An action without `[ModelContextProtocol]` isn't listed, and calling it fails like calling an unknown tool. See [Where an action is offered](../actions.md#where-an-action-is-offered).
- The tool's description is the action's `Description`, and its input schema comes from the action's type, including `[Description]` on its properties. **These are what the agent reads to choose a tool and fill its arguments,** so write them carefully.
- Queries and stream queries are marked read-only. Commands are marked as possibly destructive, so MCP clients can ask the user before running them.

| Action kind | Tool result |
|---|---|
| Command, query | `{ "result": ... }`, with `null` for a command without a result |
| Stream query | `{ "items": [...], "truncated": false }`, cut at `MaxStreamResults` |

Stream queries are collected into one result, because a tool call returns once. They stop at `MaxStreamResults` (100 by default), and `truncated` tells the agent there's more.

### Host-provided properties

A property marked [`[HostProvided]`](../actions.md#host-provided-properties), such as the tenant of the user's session, is left out of the tool's input schema, so the agent is never asked for it. A tool call that gives it anyway, in any letter case, is refused with `'tenantId' is set by the server and cannot be given.`, and the action doesn't run. It's refused rather than silently dropped, so neither the agent nor the user believes they chose the value, and an agent steered into choosing it is visible.

The MCP server dispatches in-process, so it must set the value itself, in a [decorator of `IActionDispatcher`](../actions.md#filling-the-value). The tool checks the agent's arguments first, and only then calls the dispatcher, so the decorator's value is never refused:

1. The tool refuses arguments that contain a host-provided property.
2. It calls `IActionDispatcher`, which is your decorator.
3. The decorator sets the value from the session, replacing any value in the arguments.
4. The inner `ActionDispatcher` deserializes the action and sends it.

> **Don't write host-provided values into `tools/call` arguments,** for example in MCP middleware: the tool would refuse every call. Set them in the decorator instead.

## Options

```csharp
.WithMediatorActions(o =>
{
    o.Filter = a => a.Name.StartsWith("invoices.");   // offer only some of the [ModelContextProtocol] actions
    o.MaxStreamResults = 50;
    o.SerializerOptions = mySerializerOptions;         // default: web defaults (camelCase)
    o.ErrorMessage = ...;                              // or ErrorContent, not both: see Errors
});
```

Offer agents only what they need. Every tool you offer may be called, with any arguments the agent chooses.

## Errors

Errors are returned as tool results marked with `isError: true`, so their text is exactly the message, with nothing added by the MCP SDK. Invalid arguments, missing authentication and denied access are reported to the agent with fixed, safe messages. Invalid arguments name the argument that failed, when it's known, so the agent can correct its call:

```text
The argument 'amount' of action 'invoices.create' is not valid.
```

A missing argument, or `null` where it isn't accepted, reads "... is required." instead.

See [Invalid arguments](../actions.md#invalid-arguments) for how the argument is spelled. Any other exception is reported as a generic error that reveals nothing, unless `ErrorMessage` returns a safe message for it. `ErrorMessage` also receives `InvalidActionArgumentsException`, `AuthenticationRequiredException` and `AccessDeniedException` first, so you can report them with your own codes, and `null` keeps Mediator's text. The result is an error whatever it returns:

```csharp
.WithMediatorActions(o => o.ErrorMessage = e => e switch
{
    InvalidActionArgumentsException { Field: { } field } => $"invalid: {field}",
    AuthenticationRequiredException => "authentication_required",
    AccessDeniedException => "access_denied",
    ValidationException v => string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),   // never AttemptedValue
    InvoiceNotFoundException => "The invoice doesn't exist.",
    _ => null,
})
```

Don't say why access was denied, such as a missing role or who owns a resource: that tells an agent how to get in.

### Structured errors

To return JSON that agents can parse, like HTTP callers read `ProblemDetails`, use `ErrorContent` instead of `ErrorMessage`. It receives the same exceptions, and the object it returns, serialized with `SerializerOptions`, becomes the result's `structuredContent` and, as JSON, its text:

```csharp
.WithMediatorActions(o => o.ErrorContent = e => e switch
{
    InvalidActionArgumentsException { Field: { } field } invalid => new
    {
        code = "invalid_arguments",
        errors = new Dictionary<string, string>
        {
            [field] = invalid.Reason is InvalidActionArgumentReason.Invalid ? "invalid" : "required",
        },
    },
    ValidationException v => new
    {
        code = "validation_failed",
        errors = v.Errors.ToDictionary(f => f.PropertyName, f => f.ErrorMessage),   // never AttemptedValue
    },
    AuthenticationRequiredException => new { code = "authentication_required" },
    AccessDeniedException => new { code = "access_denied" },
    _ => null,
})
```

```json
{"code":"invalid_arguments","errors":{"amount":"required"}}
```

The value must serialize to a JSON object, as MCP requires for structured content; anything else is reported as a generic error. `null` keeps Mediator's text, which has no structured content. Setting both `ErrorMessage` and `ErrorContent` fails at startup.

Good error messages help agents correct themselves: "Amount: must be greater than 0" lets the agent retry with a valid amount.

The messages Mediator writes never contain argument values, .NET type names, stack traces or paths. They're meant for developers and agents, not for display in a user interface.

> **Security:** return only text you wrote. An exception's `Message` is for logs, including your own exceptions': a message that later gains an id or a value would reach the agent without anyone changing the hook. For invalid arguments, never return `InnerException.Message`, which can contain .NET type names and property names the agent chose.

## Security

> **Security:** an AI agent acts for a user, so it must never get more permissions than that user. Consider giving agents a narrower principal, for example without permission to cancel invoices, and mark `[ModelContextProtocol]` only the actions they need.

> **Security:** text an agent reads, including tool results and error messages, can steer its behaviour. That's why this adapter never echoes input or exception details, and why your `ErrorMessage` hook must not either.

## Configuring a client

For a stdio server, MCP clients need the command that starts it. For example, for Claude Code, in `.mcp.json`:

```json
{
  "mcpServers": {
    "invoicing": {
      "command": "dotnet",
      "args": ["run", "--project", "samples/Invoicing/Invoicing.McpServer"],
      "env": { "INVOICING_USER": "ana", "INVOICING_ROLES": "Finance" }
    }
  }
}
```
