# 19. Authorization decisions are available to the dispatch, and observed

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Authorizers return decisions with details ([0018](0018-authorizers-return-decisions.md)). Two readers need them:
- the handler, and behaviours after authorization, for example to apply the "browse-only, watermark required" constraints of the decision that allowed a read, without evaluating the rules again;
- an audit log that records **every** decision, allowed or denied, with its reason.

For the handler, a scoped service holding the decision was considered. It breaks when a handler sends another command in the same scope, which would overwrite the outer decision, and when two dispatches run in parallel in one scope. An ambient value that flows with the asynchronous call, `AsyncLocal<T>`, handles both, and ASP.NET Core's `IHttpContextAccessor` uses the same pattern.

For auditing, logging from a custom behaviour was considered. A behaviour can't see why an action was denied, because it only gets the exception, and it can't see decisions made by the authorization behaviour.

## Decision

- `IAuthorizationContext.Current` returns the `AuthorizationResult` of the action being handled: the combined decision, with each authorizer's type and decision, and `TryGetDetails<T>` to read details by type. It's backed by an `AsyncLocal`, set by `AuthorizationBehavior` before `next`:
  - a nested action sees its own decision, and the outer one again afterwards;
  - parallel dispatches see their own decisions;
  - an anonymous action sees `null`, even when sent from an authorized one;
  - message handlers see the decision of the action that published the message;
  - a stream query sees its decision for every result. Async iterators restore their consumer's context on every `MoveNextAsync`, so the behaviour sets the decision again before each one.
- `AuthorizationContext` is registered with `services.AddAuthorizationContext()`, like `AddHttpContextAccessor()`. `MediatorOptions` can't register services, so `AddAuthorizationBehavior()` can't do it.
- Every `IAuthorizationObserver`, registered with `AddAuthorizationObserver<T>()`, is told every outcome, before the action runs or the exception is thrown: `Allowed`, `Denied` (including no authorizer), `AuthenticationRequired` and `Failed` (an authorizer threw). The event carries the action, the principal, the result and the exception.
- If an observer throws, the action doesn't run and the observer's exception propagates, even when access was allowed. An evidence-grade audit log needs every executed action to be recorded, so an action that can't be audited doesn't run.
- Anonymous actions aren't authorized, so they aren't observed.

## Consequences

- Handlers can rely on the decision that allowed them, instead of querying the rules again.
- When the audit store is down, every authorized action fails. This is the intended trade-off: availability for auditability. An application that prefers availability catches and logs inside its observer.
- An observer's exception replaces the authorization exception, so a denied action whose audit fails is reported as an unexpected error, not a 403. Either way it doesn't run.
- When authorization runs inside a command's transaction ([0020](0020-commands-authorized-inside-their-transaction.md)), a denial rolls the transaction back, including anything an observer wrote through the same `DbContext`. Observers that must keep denials write through their own connection. A retried transaction authorizes, and observes, again.
- The behaviour allocates a little more per authorized dispatch: the result, its decision list, and the event when observers are registered. Dispatch without behaviours is unaffected ([0006](0006-cached-generic-dispatchers.md)).
