# 9. Messages run immediately, inside the caller's transaction

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

When a command's handler publishes a message, its handlers could run at different moments:
1. immediately, when `PublishAsync` is awaited, inside the command's transaction;
2. after the handler returns, still inside the transaction;
3. after the transaction commits, only for handlers marked as such, for example with `[AfterCommit]`.

Options 2 and 3 were considered. Deferring changes what `await PublishAsync` means: it would complete before any handler ran, and a failure after the commit would report an error for a command that actually succeeded. It also needs an ambient context, which costs an allocation per dispatch ([0006](0006-cached-generic-dispatchers.md)).

## Decision

Message handlers run immediately, in registration order, when `PublishAsync` is awaited, and the first exception stops the rest. Inside a transactional command, they run inside its transaction. There is no deferral and no `[AfterCommit]`.

## Consequences

- `await PublishAsync` means every handler has run, and a failing handler rolls back the whole command.
- A message handler with external side effects, such as an email or an HTTP call, runs even if the command's transaction later rolls back, and runs again on retries ([0010](0010-transaction-retries-and-nesting.md)). Developers must know this: it's documented in the transactions guide and the XML docs.
- A durable alternative, a transactional outbox, can be added later as a new feature without changing this behaviour.
