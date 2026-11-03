# Security

Every security consideration in this manual, as one checklist. Each item links to the guide that explains it. To report a vulnerability in the library itself, follow [SECURITY.md](../SECURITY.md).

## Authorization

- [ ] **The authorization behaviour is registered,** before validation, and either before the transaction or, when access can change while a request runs, after it with `RequireTransaction` and `AuthenticationBehavior` first. → [Behaviours](behaviors.md#order)
- [ ] **Audit observers write through their own connection,** so denials inside a transaction aren't rolled back with it. → [Authorization](authorization.md#auditing-every-decision)
- [ ] **Transaction initializers take the tenant from the authenticated user,** or check membership of a tenant taken from the request. → [Transactions](transactions.md#preparing-each-transaction)
- [ ] **Every action has an authorizer,** or is deliberately marked `[AllowAnonymousAccess]`. Authorization fails closed, so a missing authorizer denies the action, but a test catches it earlier. → [Testing](testing.md#every-action-has-an-authorizer)
- [ ] **Authorizers check the action's arguments** where it matters, such as ownership of the invoice being cancelled, not just the user's role. → [Authorization](authorization.md#authorizers)
- [ ] **Exactly one `IPrincipalAccessor`** fits the host: HTTP for APIs and remote MCP servers, fixed for CLIs and local MCP servers. → [Authorization](authorization.md#the-current-user)
- [ ] **Messages are never built from user input.** They aren't authorized. Send a command, and publish the message from its handler. → [Authorization](authorization.md#messages)

## Errors and data exposure

- [ ] **Validation errors are reported without `AttemptedValue`,** only the property name and message. → [Validation](validation.md#reporting-errors-safely)
- [ ] **Error hooks (`MapException`, `ErrorMessage`) return only text you wrote,** never an exception's `Message`, even for your own exceptions: it's for logs, and a message that later gains an id or a value would reach callers without anyone changing the hook.
- [ ] **Hooks report invalid arguments from `Field`,** never from `InvalidActionArgumentsException`'s message or `InnerException.Message`, which can contain .NET type names and property names the caller chose. → [Actions](actions.md#invalid-arguments)
- [ ] **`CustomizeProblemDetails` adds no input or exception details,** because it also runs on Mediator's own `400`, `401` and `403` responses. → [ASP.NET Core](hosts/aspnetcore.md#adding-fields-to-mediators-responses)
- [ ] **ASP.NET Core uses `UseExceptionHandler` in production,** so unhandled exceptions become a `500` without details. → [ASP.NET Core](hosts/aspnetcore.md#errors)
- [ ] **Behaviours and logs record type names, not requests or results,** which can contain personal data. → [Behaviours](behaviors.md#writing-a-behaviour)
- [ ] **Each action is offered only by the hosts that need it,** with `[Api]`, `[CommandLine]` and `[ModelContextProtocol]`. Commands only your code runs have no `[Action]`. → [Actions](actions.md#where-an-action-is-offered)
- [ ] **Action names and descriptions reveal nothing internal.** They're visible to every caller of a host that offers the action, in OpenAPI, CLI help and MCP tool lists. → [Actions](actions.md#descriptions)

## Hosts

- [ ] **API endpoints require authentication** with `RequireAuthorization()` on the action group, as defence in depth. → [ASP.NET Core](hosts/aspnetcore.md#the-current-user)
- [ ] **A CLI doesn't hold credentials users can read.** Otherwise the rules only apply while users go through the CLI. → [Command line](hosts/command-line.md#the-current-user)
- [ ] **AI agents get no more permissions than their user,** ideally fewer, and `Filter` offers only the tools they need. → [Model Context Protocol](hosts/model-context-protocol.md#security)
- [ ] **A remote MCP server authenticates every request** with `app.MapMcp().RequireAuthorization()`. → [Model Context Protocol](hosts/model-context-protocol.md#transports)

## Transactions and side effects

- [ ] **External calls inside transactional commands are idempotent,** or moved to `[NonTransactional]` commands, because retries and rollbacks don't undo them. → [Transactions](transactions.md#side-effects-and-retries)
- [ ] **Important records have unique constraints,** so a retry after an ambiguous commit fails instead of duplicating data. → [Transactions](transactions.md#side-effects-and-retries)

## What the library guarantees

These hold whatever your configuration, and are covered by the library's tests:
- An action with no authorizer is denied.
- An action a host doesn't offer fails there like an unknown one, so it can't be discovered.
- An action that no host offers, or that has no description, fails at startup.
- The host adapters never echo the caller's input, or the message of an exception they don't recognise.
- Telemetry never records exception messages, requests or results.
- Invalid action configurations, such as a message marked as an action, fail at startup.

The reasons behind each are in the [decision records](adr/README.md).
