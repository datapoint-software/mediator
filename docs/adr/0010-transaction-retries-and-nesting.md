# 10. `TransactionBehavior` retries the whole command, and nested commands join

- **Status:** Superseded by [0024](0024-read-only-query-transactions.md)
- **Date:** 2026-10-31

## Context

EF Core can retry transient failures, such as deadlocks and dropped connections, through an execution strategy, for example `EnableRetryOnFailure`. When retries are on, EF Core requires a user-initiated transaction to run inside the strategy, as a unit that can be repeated. A command can also send other commands from its handler.

An opt-in `[Retryable]` attribute was considered, so that commands calling external APIs would never be repeated. It was rejected in favour of documenting the behaviour.

## Decision

- `TransactionBehavior<TDbContext>` runs each command in a transaction through the context's execution strategy. It begins the transaction, runs the rest of the pipeline and the handler, calls `SaveChangesAsync` once, and commits.
- On a transient failure, the strategy runs the **whole command again**: handler and message handlers included, after rolling back and clearing the change tracker.
- A command sent while a transaction is already open joins it, even if it's marked `[NonTransactional]`.
- `[NonTransactional]` commands run without a transaction, and their handlers save changes themselves.
- Queries, stream queries and messages are not affected.

## Consequences

- Database changes through the same `DbContext` are never duplicated by a retry, because they're rolled back first.
- External calls, such as HTTP requests, emails or queue messages, made inside a transactional command can happen more than once. They must be idempotent, for example with an idempotency key, or moved to a `[NonTransactional]` command sent from the host, which saves before calling out.
- If the connection drops while committing, the commit may have succeeded on the server, and the retry saves the same changes again. A unique constraint turns that into an error instead of a duplicate.
- These caveats are documented in the EF Core README, the transactions guide and the XML docs.
