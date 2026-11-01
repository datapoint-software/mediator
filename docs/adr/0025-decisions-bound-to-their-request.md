# 25. Authorization decisions are bound to their request

- **Status:** Accepted
- **Date:** 2026-11-01
- **Supersedes:** [0018](0018-authorizers-return-decisions.md), [0019](0019-decisions-are-available-and-observed.md)

## Context

[0018](0018-authorizers-return-decisions.md) made authorizers return decisions with details, and [0019](0019-decisions-are-available-and-observed.md) made the decision available through `IAuthorizationContext.Current` and to observers. The first application to use them, a file sharing platform for law enforcement and government, reviewed both before release and found four gaps:
- `Current` is an ambient slot. A handler can't tell whether the decision belongs to its own request or to an outer one, and message handlers saw the publisher's decision, which invites treating it as their own, although messages aren't authorized ([0008](0008-messages-are-not-authorized.md)).
- When an authorizer threw, observers got no decisions at all, so an audit of failures lacked the reasons gathered so far.
- Some denials need an outcome of the application's own, such as "step-up authentication required". Throwing it from an authorizer made it a `Failed` outcome, with no decision.
- With several authorizers, all of them must still allow, and stopping at the first denial was confirmed as sufficient.

This record restates the decisions of 0018 and 0019 with these changes.

## Decision

Decisions:
- `IAuthorizer<TAction>.AuthorizeAsync` returns an `AuthorizationDecision`: `IsAllowed`, application-defined `Details` (`object?`) and an optional `Exception`. It's created with `Allow(details)`, `Deny(details, exception)` or `AllowIf(condition)`. Decisions without details or exception are shared instances. A generic decision type was rejected, because the behaviour only knows the action type and an action's authorizers return different detail types.
- Authorization fails closed:
  - with no authenticated user, `AuthenticationRequiredException` is thrown;
  - an action with no authorizer is denied with `AccessDeniedException`;
  - all authorizers must allow; they run in registration order and stop at the first denial;
  - a `null` decision is a denial;
  - a denial throws the decision's `Exception` if it has one, and `AccessDeniedException` otherwise;
  - an authorizer that throws stops the action, and its exception propagates unchanged;
  - `[AllowAnonymousAccess]` actions skip the checks. The attribute isn't inherited.
- Details never reach the caller. `AccessDeniedException` messages are generic. A decision's own exception is treated by adapters like any exception of the application: reported only if the application maps it ([0012](0012-adapters-never-echo-input-or-exceptions.md), [0013](0013-adapters-match-authorization-exceptions-by-name.md)).

The decision of the current request:
- `AuthorizationResult` holds the authorized `Action` instance, `IsAllowed`, and each authorizer's decision. `TryGetDetails<T>` reads details by type.
- `IAuthorizationContext.For(action)` returns the current result only when it was made for that exact instance (reference equality). Handlers use it. `Current` returns it without the check, for behaviours and observers.
- The result flows with the asynchronous call (`AsyncLocal`), so nested and parallel dispatches see their own. Anonymous actions and **message handlers see `null`**: the behaviour clears it around them, without authorizing them. For stream queries it's set before each `MoveNextAsync`, because async iterators run each step in their consumer's context.
- `AuthorizationContext` is registered with `AddAuthorizationContext()`.

Observing:
- Every `IAuthorizationObserver` (registered with `AddAuthorizationObserver<T>()`) is told every outcome, before the action runs or the exception is thrown: `Allowed`, `Denied`, `AuthenticationRequired` or `Failed`.
- A `Failed` event carries the exception and a denied result with the decisions made before the authorizer threw.
- If an observer throws, the action doesn't run, even when it was allowed: an action that can't be audited doesn't run.

## Consequences

- A handler that reads a decision with `For` can't use one made for another request. A behaviour that replaces the request after authorization makes `For` return `null`, and the handler fails closed. The documentation says this is intended.
- A message handler that needs the publisher's decision must receive what it needs in the message. A `Publisher` property was considered and left out until there's a need.
- Applications can raise their own exceptions for special denials, and must map them in each host, or callers get a generic error.
- When the audit store is down, protected actions fail. This trades availability for auditability.
- Details and their exceptions can be sensitive, so they must be protected wherever they're stored.
