# Authorization

`Datapoint.Mediator.Authorization` checks, before every command and query runs, that the current user may run it. The same rules apply from an API, a CLI and an MCP server.

```bash
dotnet add package Datapoint.Mediator.Authorization.DependencyInjection --prerelease
```

## Authorizers

An authorizer decides whether a user may run one action. Write it in your business layer:

```csharp
public sealed class InvoiceAuthorizer : IAuthorizer<CreateInvoice>, IAuthorizer<GetInvoice>
{
    public ValueTask<bool> IsAuthorizedAsync(CreateInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(principal.IsInRole("Finance"));

    public ValueTask<bool> IsAuthorizedAsync(GetInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(true);   // any signed-in user
}
```

An authorizer receives the action itself, so it can decide on its arguments, such as "only amounts up to 1000", and it can use injected services, such as a database, to check ownership.

Register the authorizers, the current user and the behaviour:

```csharp
services.AddAuthorizersFromAssemblyContaining<InvoiceAuthorizer>();
services.AddHttpPrincipalAccessor();   // or AddFixedPrincipalAccessor(...): see "The current user"
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()        // before validation
    .AddValidationBehavior());
```

## The rules

Authorization **fails closed**: when in doubt, it refuses.

| Situation | Result |
|---|---|
| No current user, or a user without an authenticated identity | `AuthenticationRequiredException` (HTTP 401, CLI exit code 3) |
| The action has **no authorizer** | `AccessDeniedException` (HTTP 403, CLI exit code 4) |
| Any of the action's authorizers returns `false` | `AccessDeniedException` |
| Every authorizer returns `true` | the action runs |
| The action is marked `[AllowAnonymousAccess]` | the action runs, without any check |

- Forgetting an authorizer denies the action, rather than exposing it. You find out in development, not from a leak.
- When an action has several authorizers, such as one for roles and one for ownership, all of them must allow it.
- Commands, queries and stream queries are authorized. For stream queries, the check runs before the first result.
- `[AllowAnonymousAccess]` isn't inherited: a derived type must be marked too.

```csharp
[AllowAnonymousAccess]
public sealed record GetExchangeRates : IQuery<IReadOnlyList<ExchangeRate>>;
```

## Messages

Messages are **not** authorized: their handlers run without any check. That's safe as long as messages are published only by your own code, after an authorized command:

```csharp
// In CancelInvoiceHandler, which was authorized:
await mediator.PublishAsync(new InvoiceCancelled(invoice.Id, command.Reason), ct);
```

> **Security:** never publish a message built directly from user input, such as in an endpoint or a custom MCP tool. An attacker could trigger any message handler without permission. Send an authorized command instead, and publish the message from its handler.

## The current user

The behaviour asks `IPrincipalAccessor` who the current user is. Register exactly one, depending on the host:

| Host | Registration | Package |
|---|---|---|
| ASP.NET Core API, MCP server over HTTP | `services.AddHttpPrincipalAccessor()` | `Datapoint.Mediator.Authorization.AspNetCore` |
| CLI, MCP server over stdio | `services.AddFixedPrincipalAccessor(principal)` | `Datapoint.Mediator.Authorization.DependencyInjection` |

- **HTTP** returns the request's user, as set by ASP.NET Core authentication, so call `app.UseAuthentication()` before your endpoints. Outside a request, there's no user.
- **Fixed** returns the same user for the whole process, such as the user signed in when the CLI started. Pass `null` when nobody is signed in: actions that need a user then fail with "authentication required".
- Registering a second, different accessor throws `InvalidOperationException`, so a misconfiguration can't silently change who commands run as.
- For anything else, implement `IPrincipalAccessor` yourself and register it as scoped.

### Background work in an API

A hosted service or a queue consumer in an ASP.NET Core app has no HTTP request, so `HttpPrincipalAccessor` returns no user, and protected actions fail with `AuthenticationRequiredException`. Options:
- mark actions that are only ever run by the system with `[AllowAnonymousAccess]`, and don't offer them from hosts (`Hidden = true`, see [Actions](actions.md#hidden-actions));
- or write your own `IPrincipalAccessor` that returns the request's user during a request, and a service account otherwise.

See [Use cases](use-cases.md#background-jobs).

## Hosts and AI agents

> **Security:** a CLI that connects to the database with credentials the user can read only enforces these rules while the user goes through the CLI. For real enforcement, keep credentials on a server: have the CLI call your API.

> **Security:** an AI agent acts for a user, so it must never get more permissions than that user. Consider a narrower principal for agents, for example read-only, and remember that it can call every tool you offer with any arguments.

See [Security](security.md) for the full checklist.
