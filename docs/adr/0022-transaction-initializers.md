# 22. Transaction initializers run after BEGIN, on every attempt and on join

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

PostgreSQL row-level security can isolate tenants with a transaction-local setting, `set_config('app.tenant_id', …, true)`, applied after the transaction begins and before any query. A setting applied before `TransactionBehavior` begins its transaction is lost, a retried transaction ([0010](0010-transaction-retries-and-nesting.md)) needs it again, and authorization running inside the transaction ([0020](0020-authorization-inside-the-transaction.md)) must see it.

EF Core's `DbTransactionInterceptor.TransactionStarted` was considered. Interceptors are configured on `DbContextOptions`, which makes the current user, a scoped service, awkward to reach and breaks with context pooling, and they can't tell which request a transaction is for.

## Decision

- `ITransactionInitializer<TDbContext>.InitializeAsync(TDbContext, TransactionInitialization, CancellationToken)`, registered with `AddTransactionInitializer<TDbContext, TInitializer>()`.
- `TransactionBehavior` runs every initializer, in registration order, right after it begins a transaction and after the read-only statement, before the rest of the pipeline, for commands and query transactions. They run inside the execution strategy, so again on every retry.
- They also run when a request joins an outer request's transaction, with `IsJoined` set, so an application can check that the nested request matches, such as the same tenant. Initializers with nothing to check return early.
- `TransactionInitialization` carries the `Request`, `IsReadOnly`, `IsJoined`, `Attempt` and `AttemptId`.
- If an initializer throws, the request fails and the transaction is rolled back.

## Consequences

- Row-level security and similar per-transaction settings apply to everything that runs in the transaction, including authorization and validation.
- Initializers run before authorization, so they may see requests from callers who will be refused. They must take values from the authenticated user, not from the request alone, and policies must match no rows when a setting is missing. A tenant taken from the request must be checked against the user's memberships before it's applied.
- Transactions an application begins itself aren't prepared ([0023](0023-whoever-begins-a-transaction-commits-it.md)).
