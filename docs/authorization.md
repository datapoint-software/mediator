# Authorization

`Datapoint.Mediator.Authorization` checks, before every command and query runs, that the current user may run it. The same rules apply from an API, a CLI and an MCP server.

```bash
dotnet add package Datapoint.Mediator.Authorization.DependencyInjection --prerelease
```

## Authorizers

An authorizer decides whether a user may run one action, and returns an `AuthorizationDecision`. Write it in your business layer:

```csharp
public sealed class InvoiceAuthorizer : IAuthorizer<CreateInvoice>, IAuthorizer<GetInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(CreateInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.AllowIf(principal.IsInRole("Finance")));

    public ValueTask<AuthorizationDecision> AuthorizeAsync(GetInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.Allow());   // any signed-in user
}
```

An authorizer receives the action itself, so it can decide on its arguments, such as "only amounts up to 1000", and it can use injected services, such as a database, to check ownership. Authorizers only read: they must not change data, because they can run inside the command's transaction.

### Decisions and their details

A decision can carry **details**: any object of yours that explains it, such as the share, role or label rule that applied, or the constraints the handler must respect:

```csharp
public sealed record ReadGrant(string ShareId, string Role, bool BrowseOnly, bool Watermark);

public sealed class DownloadItemAuthorizer(FileShareDbContext db) : IAuthorizer<DownloadItem>
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(DownloadItem action, ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var share = await db.Shares.AsNoTracking()
            .Where(s => s.ItemId == action.ItemId && s.UserId == principal.FindFirstValue("sub"))
            .FirstOrDefaultAsync(ct);

        return share is null
            ? AuthorizationDecision.Deny(new NoShare(action.ItemId))
            : AuthorizationDecision.Allow(new ReadGrant(share.Id, share.Role, share.BrowseOnly, share.Watermark));
    }
}
```

| Create it with | Meaning |
|---|---|
| `AuthorizationDecision.Allow(details)` | allowed; `details` is optional |
| `AuthorizationDecision.Deny(details)` | denied; `details` is optional |
| `AuthorizationDecision.AllowIf(condition)` | allowed when `condition` is `true`, without details |

> **Security:** details are for your application and its audit log. They **never** reach the caller: the exception's message doesn't include them, and the API, CLI and MCP adapters only say 401 or 403. Don't copy them into your own error responses either. They can be sensitive, such as a classification label, so protect them wherever you store them.

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
| Any of the action's authorizers denies it, or returns `null` | `AccessDeniedException` |
| An authorizer throws | its exception propagates, and the action doesn't run |
| An [observer](#auditing-every-decision) throws | its exception propagates, and the action doesn't run, even if it was allowed |
| Every authorizer allows it | the action runs |
| The action is marked `[AllowAnonymousAccess]` | the action runs, without any check |

- Forgetting an authorizer denies the action, rather than exposing it. You find out in development, not from a leak.
- When an action has several authorizers, such as one for roles and one for ownership, all of them must allow it. They run in registration order and stop at the first denial.
- Commands, queries and stream queries are authorized. For stream queries, the check runs before the first result.
- `[AllowAnonymousAccess]` isn't inherited: a derived type must be marked too.

```csharp
[AllowAnonymousAccess]
public sealed record GetExchangeRates : IQuery<IReadOnlyList<ExchangeRate>>;
```

## Reading the decision in a handler

A handler can read the decision that allowed it, with its details, instead of evaluating the rules again. Register the context once:

```csharp
services.AddAuthorizationContext();
```

```csharp
public sealed class DownloadItemHandler(IAuthorizationContext authorization, FileStore files)
    : IQueryHandler<DownloadItem, FileDownload>
{
    public async ValueTask<FileDownload> HandleAsync(DownloadItem query, CancellationToken ct = default)
    {
        // Fail closed: without a grant, don't guess the constraints.
        if (authorization.Current?.TryGetDetails<ReadGrant>(out var grant) != true)
        {
            throw new InvalidOperationException("DownloadItem was not authorized with a ReadGrant.");
        }

        return grant.BrowseOnly ? FileDownload.PreviewOnly(query.ItemId) : await files.OpenAsync(query.ItemId, grant.Watermark, ct);
    }
}
```

- `Current` is an `AuthorizationResult`: `IsAllowed`, the `Decisions` of each authorizer (its type and its decision), and `TryGetDetails<T>`, which returns the first details of type `T`.
- The decision flows with the call, like `IHttpContextAccessor`: a command sent from a handler sees its own decision, then the outer one again; parallel dispatches see their own.
- Behaviours registered after authorization see it too. Message handlers see the decision of the action that published the message.
- `[AllowAnonymousAccess]` actions see `null`.

## Auditing every decision

An `IAuthorizationObserver` is told every outcome, allowed or not, with the reasons:

```csharp
public sealed class AuthorizationAuditor(AuditLog audit) : IAuthorizationObserver
{
    public async ValueTask OnAuthorizedAsync(AuthorizationEvent authorization, CancellationToken ct = default)
    {
        await audit.WriteAsync(new AuditEntry(
            Action: authorization.Action.GetType().Name,              // the type, never the action's contents
            User: authorization.Principal?.FindFirstValue("sub"),
            Outcome: authorization.Outcome.ToString(),
            Reasons: authorization.Result?.Decisions.Select(d => new AuditReason(d.AuthorizerType.Name, d.Decision.IsAllowed, d.Decision.Details)).ToArray() ?? [],
            Error: authorization.Exception?.GetType().FullName), ct);
    }
}
```

```csharp
services.AddAuthorizationObserver<AuthorizationAuditor>();
```

| `Outcome` | When | `Result` |
|---|---|---|
| `Allowed` | every authorizer allowed it | every decision |
| `Denied` | an authorizer denied it, or it has none | the decisions up to the denial; empty with no authorizer |
| `AuthenticationRequired` | no signed-in user | `null` |
| `Failed` | an authorizer threw; `Exception` holds it | `null` |

- Observers run in registration order, after the authorizers and before the action runs or the exception is thrown.
- **If an observer throws, the action doesn't run**, even when it was allowed: an action that can't be audited doesn't run. If you prefer availability, catch and log inside your observer.
- `[AllowAnonymousAccess]` actions aren't authorized, so they aren't observed.
- Don't log `Action` as it is: it holds the caller's input, which can be personal data.

## Explaining access

To show a user why they can or can't do something, run the authorizers yourself in a query of your own. Give that query its own authorizer, because explanations reveal your rules:

```csharp
public sealed record ExplainDownload(Guid ItemId) : IQuery<AccessExplanation>;

public sealed class ExplainDownloadHandler(IEnumerable<IAuthorizer<DownloadItem>> authorizers, IPrincipalAccessor user)
    : IQueryHandler<ExplainDownload, AccessExplanation>
{
    public async ValueTask<AccessExplanation> HandleAsync(ExplainDownload query, CancellationToken ct = default)
    {
        var reasons = new List<object?>();
        var allowed = false;

        foreach (var authorizer in authorizers)
        {
            var decision = await authorizer.AuthorizeAsync(new DownloadItem(query.ItemId), user.Principal!, ct);
            reasons.Add(decision.Details);
            allowed = decision.IsAllowed;

            if (!allowed)
            {
                break;   // like the behaviour: stop at the first denial
            }
        }

        return new AccessExplanation(allowed, reasons);   // no authorizer: allowed stays false
    }
}
```

Keep the same rules as the behaviour: no authorizer means denied, and all must allow. Translate details into messages that are safe for that user to see.

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
