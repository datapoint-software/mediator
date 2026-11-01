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

- Each action becomes a tool, named with dots replaced by underscores, such as `invoices_create`, because many AI model APIs don't accept dots in tool names.
- The tool's description is the action's `Description`, and its input schema comes from the action's type, including `[Description]` on its properties. **These are what the agent reads to choose a tool and fill its arguments,** so write them carefully.
- Queries and stream queries are marked read-only. Commands are marked as possibly destructive, so MCP clients can ask the user before running them.

| Action kind | Tool result |
|---|---|
| Command, query | `{ "result": ... }`, with `null` for a command without a result |
| Stream query | `{ "items": [...], "truncated": false }`, cut at `MaxStreamResults` |

Stream queries are collected into one result, because a tool call returns once. They stop at `MaxStreamResults` (100 by default), and `truncated` tells the agent there's more.

## Options

```csharp
.WithMediatorActions(o =>
{
    o.Filter = a => a.Name.StartsWith("invoices.");   // offer only some actions
    o.MaxStreamResults = 50;
    o.SerializerOptions = mySerializerOptions;         // default: web defaults (camelCase)
    o.ErrorMessage = ...;                              // see Errors
});
```

Offer agents only what they need. Every tool you offer may be called, with any arguments the agent chooses.

## Errors

Invalid arguments, missing authentication and denied access are reported to the agent with fixed, safe messages. Any other exception is reported as a generic error that reveals nothing, unless `ErrorMessage` returns a safe message for it:

```csharp
.WithMediatorActions(o => o.ErrorMessage = e => e switch
{
    ValidationException v => string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),   // never AttemptedValue
    InvoiceNotFoundException => e.Message,
    _ => null,
})
```

Good error messages help agents correct themselves: "Amount: must be greater than 0" lets the agent retry with a valid amount.

## Security

> **Security:** an AI agent acts for a user, so it must never get more permissions than that user. Consider giving agents a narrower principal, for example without permission to cancel invoices, and use `Filter` to offer only the tools they need.

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
