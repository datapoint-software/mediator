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

public sealed class DownloadItemAuthorizer(AppDbContext db) : IAuthorizer<DownloadItem>
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

> **Security:** details are for your application and its audit log. They **never** reach the caller: the exceptions don't carry them, and the API, CLI and MCP adapters only say 401 or 403, in Mediator's words or your hook's. Don't copy them into your own error responses either. They can be sensitive, such as a classification label, so protect them wherever you store them.

Register the authorizers, the current user and the behaviour:

```csharp
services.AddAuthorizersFromAssemblyContaining<InvoiceAuthorizer>();
services.AddHttpPrincipalAccessor();   // or AddFixedPrincipalAccessor(...): see "The current user"
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()        // before validation
    .AddValidationBehavior());
```

### Denials of your own

Some denials aren't a plain 403, such as "step-up authentication required" or "confirm first". Pass the exception to throw instead of `AccessDeniedException`:

```csharp
return AuthorizationDecision.Deny(new StepUpRequired(Level: 2), new StepUpRequiredException(level: 2));
```

- Observers see the outcome `Denied`, with the details and the decision's `Exception`.
- Host adapters treat it like any exception of yours: map it with `MapException` (ASP.NET Core) or `ErrorMessage` (CLI, MCP), or callers get a generic error. Don't put details in its message unless your mapping is safe.
- When a transaction is retried, the same exception instance is thrown again.

## The rules

Authorization **fails closed**: when in doubt, it refuses.

| Situation | Result |
|---|---|
| No current user, or a user without an authenticated identity | `AuthenticationRequiredException` (HTTP 401, CLI exit code 3) |
| The action has **no authorizer** | `AccessDeniedException` (HTTP 403, CLI exit code 4) |
| Any of the action's authorizers denies it, or returns `null` | `AccessDeniedException`, or the [exception the decision carries](#denials-of-your-own) |
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
        // For(query) returns the decision only if it was made for this exact request.
        // Fail closed: without a grant, don't guess the constraints.
        if (authorization.For(query)?.TryGetDetails<ReadGrant>(out var grant) != true)
        {
            throw new InvalidOperationException("DownloadItem was not authorized with a ReadGrant.");
        }

        return grant.BrowseOnly ? FileDownload.PreviewOnly(query.ItemId) : await files.OpenAsync(query.ItemId, grant.Watermark, ct);
    }
}
```

- `For(request)` returns an `AuthorizationResult` only when the current decision was made for that exact instance (reference equality), and `null` otherwise. The result has the `Action`, `IsAllowed`, the `Decisions` of each authorizer (its type and its decision), and `TryGetDetails<T>`, which returns the first details of type `T`.
- `Current` returns the current decision without that check. Behaviours registered after authorization can use it, but handlers should use `For`.
- The decision flows with the call, like `IHttpContextAccessor`: a command sent from a handler sees its own decision, then the outer one again; parallel dispatches see their own.
- Message handlers aren't authorized, so they see `null`, and so do `[AllowAnonymousAccess]` actions.
- A behaviour that replaces the request with a new instance between authorization and the handler, for example to normalise it, makes `For(newInstance)` return `null`, so a handler that needs a decision fails. **That's intended:** the new instance wasn't authorized. Normalise before authorization instead, or change the instance in place.

## Auditing every decision

An `IAuthorizationObserver` is told every outcome, allowed or not, with the reasons. When authorization runs inside a transaction, a denial rolls it back, so write the audit through **its own `DbContext`**, not the one your handlers use:

```csharp
public sealed class AuthorizationAuditor(IDbContextFactory<AuditDbContext> audits) : IAuthorizationObserver
{
    public async ValueTask OnAuthorizedAsync(AuthorizationEvent authorization, CancellationToken ct = default)
    {
        await using var audit = await audits.CreateDbContextAsync(ct);   // its own connection: survives a rollback
        audit.Entries.Add(new AuditEntry
        {
            Action = authorization.Action.GetType().Name,              // the type, never the action's contents
            User = authorization.Principal?.FindFirstValue("sub"),
            Outcome = authorization.Outcome.ToString(),
            Reasons = authorization.Result?.Decisions.Select(d => new AuditReason(d.AuthorizerType.Name, d.Decision.IsAllowed, d.Decision.Details)).ToList() ?? [],
            Error = authorization.Exception?.GetType().FullName,
        });
        await audit.SaveChangesAsync(ct);
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
| `Failed` | an authorizer threw; `Exception` holds it | denied, with the decisions made before it |

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

## Authentication before the transaction

`AuthenticationBehavior` refuses requests without a signed-in user, and nothing else. Register it first, so that callers who aren't signed in are refused before a transaction begins:

```csharp
o.AddAuthenticationBehavior()   // first
```

It's told to observers as `AuthenticationRequired`. `[AllowAnonymousAccess]` actions and messages skip it. The authorization behaviour still checks authentication itself.

## Authorizing inside the transaction

By default, authorization runs before the transaction, so an action is checked against the data as it was then, and runs against the data as it is a moment later. If access can change in between, such as a share revoked or a label changed, register the transaction behaviour **before** authorization. The rules are then checked inside the same transaction as the handler, prepared by its initializers. See the [recommended order](behaviors.md#order).

To make that order a rule rather than a convention:

```csharp
services.AddTransactionContext();
services.AddMediator(o => o
    …
    .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.CommandsAndQueries));   // or Commands
```

- **At runtime:** authorizing an action of a required kind without a transaction begun by the mediator throws `InvalidOperationException`, before any authorizer runs. Stream queries are checked at first enumeration.
- **At startup:** `AddMediator` throws if an action of a required kind is marked `[NonTransactional]`, unless it's `[AllowAnonymousAccess]`.

Inside the transaction:
- a denial rolls the transaction back, so an [observer](#auditing-every-decision) must write denials through its own `DbContext`;
- a retried transaction authorizes, and observes, again: record `ITransactionContext.AttemptId` in your audit;
- authorizers must only read: anything they change would be saved with the command;
- the transaction only keeps what the authorizer read from changing at a strong enough isolation level, or with row locks: see [Transactions](transactions.md#settings).

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
- mark commands that are only ever run by the system with `[AllowAnonymousAccess]`, and don't make them actions (no `[Action]`), so no host offers them (see [Actions](actions.md#where-an-action-is-offered));
- or write your own `IPrincipalAccessor` that returns the request's user during a request, and a service account otherwise.

See [Use cases](use-cases.md#background-jobs).

## Hosts and AI agents

> **Security:** a CLI that connects to the database with credentials the user can read only enforces these rules while the user goes through the CLI. For real enforcement, keep credentials on a server: have the CLI call your API.

> **Security:** an AI agent acts for a user, so it must never get more permissions than that user. Consider a narrower principal for agents, for example read-only, and remember that it can call every tool you offer with any arguments.

See [Security](security.md) for the full checklist.
