# 13. Adapters recognise authorization exceptions by type name

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

Host adapters must turn `AuthenticationRequiredException` and `AccessDeniedException` into 401 and 403 responses, or the matching exit codes. Those types live in `Datapoint.Mediator.Authorization`. Referencing that package from every adapter would force it on apps that don't use authorization ([0004](0004-package-split.md)).

## Decision

Adapters compare the exception's full type name with `Datapoint.Mediator.Authorization.AuthenticationRequiredException` and `Datapoint.Mediator.Authorization.AccessDeniedException`, instead of referencing the types. Both exception types are `sealed`, so only the exact types match.

## Consequences

- Adapters depend only on `Abstractions`.
- Renaming or moving either exception type would silently break the mapping in every adapter. Adapter tests use the real authorization behaviour, so such a change fails those tests.
- A new host adapter must match both names the same way.
