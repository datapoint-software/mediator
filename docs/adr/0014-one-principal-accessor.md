# 14. One `IPrincipalAccessor` per application

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The authorization behaviour asks `IPrincipalAccessor` for the current user. The packages provide two: `HttpPrincipalAccessor`, for the ASP.NET Core request's user, and `FixedPrincipalAccessor`, for one user for the whole process, as in a CLI or an MCP server over stdio. With Microsoft's DI container, when two implementations are registered, the last one silently wins, so a stray registration could change who every command runs as.

## Decision

- `AddHttpPrincipalAccessor` and `AddFixedPrincipalAccessor` throw `InvalidOperationException` when a different `IPrincipalAccessor` is already registered. Registering the same one again has no effect.
- `FixedPrincipalAccessor` accepts `null`, meaning no one is signed in, so actions that need a user fail with `AuthenticationRequiredException`.
- Outside an HTTP request, `HttpPrincipalAccessor` returns `null`.

## Consequences

- Misconfigured accessors fail at startup instead of silently changing the user.
- An API's background work, such as a hosted service, has no request, so its commands fail with `AuthenticationRequiredException`, unless they're marked `[AllowAnonymousAccess]` or the app writes its own accessor. A built-in way to run background work as a service account can be added later without a breaking change.
