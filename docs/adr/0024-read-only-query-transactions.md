# 24. Queries can run in read-only transactions, opt-in

- **Status:** Accepted
- **Date:** 2026-11-01
- **Supersedes:** [0010](0010-transaction-retries-and-nesting.md)

## Context

[0010](0010-transaction-retries-and-nesting.md) decided how commands run in transactions, and that queries, stream queries and messages are not affected. An application isolating tenants with PostgreSQL row-level security and a transaction-local setting ([0022](0022-transaction-initializers.md)) needs queries in a transaction too, read-only, prepared the same way. Authorization of a query inside that transaction also needs it ([0020](0020-authorization-inside-the-transaction.md)).

Read-only isn't portable: PostgreSQL has `SET TRANSACTION READ ONLY`, which must be the first statement, and SQL Server has no read-only transactions. Detecting the provider was rejected, to keep the package provider-neutral.

Stream queries return results while they run. With a retrying execution strategy, EF Core refuses queries inside a transaction the application began, unless they run inside `ExecutionStrategy.ExecuteAsync`, and results already returned to the consumer can't be retried. Running the stream on a background task inside the strategy was rejected: the consumer's code would use the same scoped `DbContext` concurrently.

## Decision

This restates [0010](0010-transaction-retries-and-nesting.md) for commands, and adds queries.

Commands:
- `TransactionBehavior<TDbContext>` runs each command in a transaction through the context's execution strategy: it begins the transaction, runs the initializers, the rest of the pipeline and the handler, calls `SaveChangesAsync` once, and commits.
- On a transient failure, the strategy runs the **whole command again**, after rolling back and clearing the change tracker.
- A request sent while the mediator's transaction is open joins it, even if it's marked `[NonTransactional]`.
- `[NonTransactional]` commands run without a transaction, and their handlers save changes themselves.

Queries, opt-in with `TransactionBehaviorOptions.Queries = ReadOnly` ([0021](0021-transaction-behavior-options.md)):
- Queries and stream queries run in transactions at `QueryIsolationLevel`, prepared with `ReadOnlyStatement` first and then the initializers, and **always rolled back**. Changes left in the change tracker are discarded.
- Queries run through the execution strategy, so transient failures retry the whole query.
- A stream query's transaction begins at first enumeration and is rolled back when the enumeration completes, fails or is stopped. With a retrying execution strategy, a stream query is refused with `InvalidOperationException`.
- `[NonTransactional]` excludes a query or stream query.
- Messages are never wrapped. They run in the caller's transaction ([0009](0009-messages-run-in-the-callers-transaction.md)).

## Consequences

- Database changes through the same `DbContext` are never duplicated by a retry, because they're rolled back first.
- External calls made inside a transactional command can happen more than once, so they must be idempotent, or moved to a `[NonTransactional]` command. If the connection drops while committing, a retry may save the same changes again; a unique constraint turns that into an error. These caveats from 0010 still hold.
- With query transactions, every query costs a `BEGIN` and a `ROLLBACK`, and a stream holds a connection and a snapshot while it's enumerated. On PostgreSQL, applications should set `idle_in_transaction_session_timeout`.
- On SQL Server, read-only means only that changes are rolled back.
- Applications that stream with a retrying strategy must return lists from queries instead, or use a context without retries for streaming.
