# 7. Authorization fails closed

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The same actions are offered from an API, a CLI and an MCP server ([0011](0011-named-actions.md)), and AI agents can call any tool offered to them, with any arguments. If an action without an authorizer were allowed, forgetting to write one would silently expose it to every caller.

## Decision

`AuthorizationBehavior` applies to commands and queries, including stream queries:
- With no current user, or a user without an authenticated identity, it throws `AuthenticationRequiredException`.
- An action with **no authorizer is denied** with `AccessDeniedException`.
- When an action has several authorizers, all of them must allow it.
- Actions marked `[AllowAnonymousAccess]` skip the checks. The attribute isn't inherited, so a derived type isn't anonymous unless it's marked too.

`IAuthorizer<T>` returns `bool`, without a reason, which was reconsidered and kept during the public API review.

## Consequences

- A forgotten authorizer fails visibly, in development, rather than leaking data in production.
- Every new action needs an authorizer, or an explicit `[AllowAnonymousAccess]`, which reviewers can spot.
- A refusal can't explain itself to the caller. Adding reasons later would need a new interface.
