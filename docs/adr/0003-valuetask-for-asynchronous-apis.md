# 3. `ValueTask` for every asynchronous API

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

Every dispatch goes through the mediator, behaviours and a handler. Many handlers complete synchronously, for example when they only add an entity to the change tracker, and a `Task` allocates whenever it isn't already completed and cached.

## Decision

Handlers, behaviours, `IMediator`, authorizers and `IActionDispatcher` return `ValueTask` or `ValueTask<T>`. Streams use `IAsyncEnumerable<T>`.

## Consequences

- A handler that completes synchronously costs no allocation, which keeps the dispatch path cheap ([0006](0006-cached-generic-dispatchers.md)).
- Callers must follow `ValueTask`'s rules: await it once, and never await it twice or concurrently. The XML docs say "Await it only once".
- Changing these signatures to `Task` later would be a breaking change.
