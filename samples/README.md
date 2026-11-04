# Samples

## Invoicing: one business layer, three hosts

[`Invoicing/Invoicing.Domain`](Invoicing/Invoicing.Domain) is a small business layer. It defines four actions, with handlers, validators, authorizers, a message and an EF Core database. Three hosts offer the same actions, with the same rules:

| Host | Project | `invoices.create` becomes |
|---|---|---|
| HTTP API | [`Invoicing.Api`](Invoicing/Invoicing.Api/Program.cs) | `POST /api/invoices/create` |
| CLI | [`Invoicing.Cli`](Invoicing/Invoicing.Cli/Program.cs) | `invoicing invoices create --customer Ana --amount 100` |
| MCP server | [`Invoicing.McpServer`](Invoicing/Invoicing.McpServer/Program.cs) | the tool `invoices_create` |

| Action | Kind | Who may run it |
|---|---|---|
| `invoices.create` | command with a result | users with the `Finance` role |
| `invoices.get` | query | any signed-in user |
| `invoices.list` | stream query | any signed-in user |
| `invoices.cancel` | command; publishes `InvoiceCancelled`, whose handler writes an audit entry in the same transaction | users with the `Finance` role |

What to look at:
- [`InvoicingServiceCollectionExtensions.cs`](Invoicing/Invoicing.Domain/InvoicingServiceCollectionExtensions.cs): the registration every host shares, with the behaviours in the recommended order.
- [`Rules.cs`](Invoicing/Invoicing.Domain/Rules.cs): validators and authorizers.
- [`Handlers.cs`](Invoicing/Invoicing.Domain/Handlers.cs): handlers that don't save changes themselves, a stream query, and a message published inside a transaction.
- Each host's `Program.cs`: how it finds the current user, and how it reports validation errors without echoing input.

All three hosts use the same SQLite file, so an invoice created from the CLI shows up in the API. By default the file is `datapoint-mediator-invoicing.db` in your local application data folder. Set `INVOICING_DATABASE` to use another path.

The samples reference the packages' source code, not the NuGet packages, so they always match the current code. CI builds them.

### CLI

The CLI acts for the user named in `INVOICING_USER`, with the comma-separated roles in `INVOICING_ROLES`. Without `INVOICING_USER`, nobody is signed in.

> **Security:** whoever sets these variables chooses the user and the roles. That's fine for a tool that runs on the user's own machine and reaches only data they could reach anyway. A CLI or MCP server shared by several users, or hosted for others, must sign them in instead and read the roles from a token.

```bash
dotnet run --project samples/Invoicing/Invoicing.Cli -- --help
```

```bash
INVOICING_USER=ana INVOICING_ROLES=Finance dotnet run --project samples/Invoicing/Invoicing.Cli -- invoices create --customer Ana --amount 100
```

```bash
INVOICING_USER=ana dotnet run --project samples/Invoicing/Invoicing.Cli -- invoices list
```

Results are JSON, and `invoices list` prints one invoice per line. The exit code is 0 on success, 2 for invalid arguments, 3 without a user, and 4 when access is denied.

### HTTP API

The API authenticates requests with JWT bearer tokens. For local development, `dotnet user-jwts` creates tokens signed with a key kept in your user secrets:

```bash
dotnet user-jwts create --project samples/Invoicing/Invoicing.Api --name ana --role Finance --output token
```

```bash
dotnet run --project samples/Invoicing/Invoicing.Api --launch-profile http
```

Then, with the token in `TOKEN`:

```bash
curl -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"customer":"Ana","amount":100}' http://localhost:5180/api/invoices/create
```

```bash
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5180/api/invoices/list
```

- `GET http://localhost:5180/openapi/v1.json` describes every action. It's only mapped in development.
- A request without a token gets 401, and a user without the `Finance` role gets 403 for `create` and `cancel`.
- Invalid arguments get 400 with each property's errors, and a missing invoice gets 404.

### MCP server

The server talks MCP over standard input and output, and acts for the user in `INVOICING_USER` and `INVOICING_ROLES`, like the CLI. To use it from an MCP client, such as Claude Code, configure it as a stdio server:

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

The agent sees four tools: `invoices_create`, `invoices_get`, `invoices_list` and `invoices_cancel`. It can never do more than the user it acts for, so consider giving it a narrower user, for example without the `Finance` role.
