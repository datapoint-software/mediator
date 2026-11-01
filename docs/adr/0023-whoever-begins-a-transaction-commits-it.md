# 23. Whoever begins a transaction saves and commits it

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

[0010](0010-transaction-retries-and-nesting.md) made nested commands join an open transaction. It didn't say what happens when the application itself began the transaction before sending a request. In that case `TransactionBehavior` joined it and didn't call `SaveChangesAsync`, which surprised a reviewer. Saving on the application's behalf would break "save once", and the behaviour can't know whether the application will roll back.

## Decision

- **Whoever begins a transaction saves and commits it.** `TransactionBehavior` saves and commits the transactions it begins. A request that joins a transaction the application began isn't saved, committed, prepared ([0022](0022-transaction-initializers.md)) or retried by the behaviour, and `ITransactionContext.IsActive` is `false` for it.
- `TransactionBehaviorOptions.ForeignTransactions = Throw` makes a request fail with `InvalidOperationException` when it finds a transaction the mediator didn't begin. The default, `Join`, keeps the rule above.
- The behaviour recognises its own transactions by the context instance that began them, so a transaction on another context isn't mistaken for its own.

## Consequences

- Applications don't need to begin transactions around a dispatch: isolation levels, preparation and read-only queries come from the behaviour ([0021](0021-transaction-behavior-options.md)), and `Throw` enforces it.
- An application that does begin its own transaction must save and commit it, as with EF Core without the mediator.
