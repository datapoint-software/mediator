# Datapoint.Mediator.Authorization

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that authorizes commands, queries and stream queries for the current user, **denying access by default**. The rules live in your domain layer, so they apply the same way whether the action comes from an API, a CLI or an MCP server.

Register it with [Datapoint.Mediator.Authorization.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.Authorization.DependencyInjection).

## Rules, in the domain layer

```csharp
public sealed class DeleteInvoiceAuthorizer : IAuthorizer<DeleteInvoice>
{
    public ValueTask<bool> IsAuthorizedAsync(DeleteInvoice command, ClaimsPrincipal user, CancellationToken ct = default) =>
        new(user.IsInRole("Finance"));
}

[AllowAnonymousAccess]
public sealed record GetPublicPrices : IQuery<PriceList>;
```

- An action with **no authorizer is denied** (`AccessDeniedException`), unless it is marked `[AllowAnonymousAccess]`.
- With no signed-in user, `AuthenticationRequiredException` is thrown (typically HTTP 401). A refused action throws `AccessDeniedException` (typically HTTP 403).
- When an action has several authorizers, all of them must allow it.
- Messages are not authorized, because they are published by the application itself.

> **Security:** never publish a message built directly from user input, for example from an API endpoint or an MCP tool call, because its handlers run without any authorization check. Send a command instead, which is authorized, and publish the message from that command's handler.

## The current user, in each host

Each host registers one `IPrincipalAccessor`. Two are ready-made:

```csharp
// API, or MCP server over HTTP: the request's user, after ASP.NET Core authentication.
// From Datapoint.Mediator.Authorization.AspNetCore.
services.AddHttpPrincipalAccessor();

// CLI, or MCP server over stdio: the user signed in at startup (for example with an OAuth device code), a service
// account, or null when no one is signed in. From Datapoint.Mediator.Authorization.DependencyInjection.
services.AddFixedPrincipalAccessor(principal);
```

Registering a second accessor throws `InvalidOperationException`, so a misconfiguration can't silently change who the current user is. For anything else, implement `IPrincipalAccessor` and register it as a scoped service.

> **Security:** a CLI that connects to the database with credentials the user can read only enforces these rules as long as the user goes through the CLI. For real enforcement, keep the credentials server-side: have the CLI call your API, or run it where users can't read its configuration. An AI agent acting through an MCP server should never receive more permissions than the user it acts for.

## Order

Register authorization **before** validation, so users who may not run an action can't learn its validation rules.
