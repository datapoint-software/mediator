# 18. Authorizers return decisions with details

- **Status:** Accepted
- **Date:** 2026-11-01
- **Supersedes:** [0007](0007-authorization-fails-closed.md)

## Context

[0007](0007-authorization-fails-closed.md) made `IAuthorizer<T>` return `bool`, and noted that adding reasons later would need a new interface. The first application with a high-security model, a file sharing platform for law enforcement and government, needs the reason behind each decision:
- its evidence-grade audit log records why access was allowed or denied: which share, role, label rule or policy applied;
- its UI explains why a user can or can't see an item;
- its handlers need the details of an allowed decision, such as "browse-only, watermark required", without evaluating the rules again.

Three ways to add reasons were considered:
1. A second interface next to the `bool` one. Rejected: two ways to write the same thing, and no defined order between them when an action has both.
2. A generic `AuthorizationDecision<TDetails>` returned by `IAuthorizer<TAction, TDetails>`. Rejected: the behaviour only knows `TAction`, so it couldn't resolve those authorizers without reflection, and an action's authorizers return different detail types, so the combined decision and the audit hook would see `object` anyway.
3. Replace the `bool` with a non-generic decision that carries an application-defined `object` as details. Chosen.

The packages are in the `4.0.0` pre-releases, where breaking changes are allowed if they're visible in review.

## Decision

- `IAuthorizer<TAction>.IsAuthorizedAsync`, returning `bool`, is replaced by `AuthorizeAsync`, returning an `AuthorizationDecision`. This is a breaking change.
- `AuthorizationDecision` has `IsAllowed` and `Details`, an application-defined `object?`. It's created with `Allow(details)`, `Deny(details)` or `AllowIf(condition)`; the last one keeps `bool` rules one line long. Decisions without details are shared instances, so they don't allocate.
- The rules of [0007](0007-authorization-fails-closed.md) are kept, so authorization still fails closed:
  - with no current user, or a user without an authenticated identity, `AuthenticationRequiredException` is thrown;
  - an action with **no authorizer is denied** with `AccessDeniedException`;
  - when an action has several authorizers, all of them must allow it. They run in order and stop at the first denial, so a refused action causes no further queries;
  - an authorizer that returns `null` denies the action;
  - an authorizer that throws stops the action, and its exception propagates unchanged, so a bug isn't hidden as a 403;
  - actions marked `[AllowAnonymousAccess]` skip the checks. The attribute isn't inherited.
- Details never reach the caller. `AccessDeniedException` keeps a generic message, without the details, and has no property that exposes them. Host adapters still only report 401 or 403 ([0012](0012-adapters-never-echo-input-or-exceptions.md), [0013](0013-adapters-match-authorization-exceptions-by-name.md)).

## Consequences

- Every authorizer must be changed: `new(principal.IsInRole("Finance"))` becomes `new(AuthorizationDecision.AllowIf(principal.IsInRole("Finance")))`. The changelog lists it as a breaking change.
- Applications can explain each decision. How handlers and audit logs read the details is decided in [0019](0019-decisions-are-available-and-observed.md).
- Readers of the details cast them to the type they expect. The compiler doesn't check that an authorizer returns the type a handler reads; tests do.
- Details may contain sensitive information, such as a label or a share. They must be treated as confidential wherever they're stored.
