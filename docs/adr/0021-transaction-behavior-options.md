# 21. `TransactionBehavior` is configured with options

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Being inside a transaction only gives consistent reads at a strong enough isolation level, and applications need different levels for commands and queries: a file sharing platform reviewing this design uses `READ COMMITTED` with its own row locks for commands, and `REPEATABLE READ` for read-only queries. Queries can now run in transactions ([0024](0024-read-only-query-transactions.md)), and applications need to refuse transactions the mediator didn't begin ([0023](0023-whoever-begins-a-transaction-commits-it.md)). That is several settings, with more likely later.

Separate overloads, such as `AddTransactionBehavior<T>(IsolationLevel)`, would multiply with each setting. The options pattern of Microsoft.Extensions.Options would put the settings in a separate call, away from the behaviour's position in the pipeline. Behaviours can now be registered with a factory ([0027](0027-startup-checks.md)).

## Decision

- `AddTransactionBehavior<TDbContext>(Action<TransactionBehaviorOptions>)` registers the behaviour with a factory, in its position in the pipeline. `AddTransactionBehavior<TDbContext>()` stays, with the defaults.
- `TransactionBehaviorOptions` has `IsolationLevel` (commands), `Queries` (`None` or `ReadOnly`), `QueryIsolationLevel`, `ReadOnlyStatement` and `ForeignTransactions` (`Join` or `Throw`). The defaults keep the earlier behaviour.
- Setters reject undefined values, so mistakes fail when registering. The behaviour copies the settings it's given.
- A level other than `Unspecified` uses EF Core's relational `BeginTransactionAsync(IsolationLevel)`, so the package depends on `Microsoft.EntityFrameworkCore.Relational`. `Unspecified` keeps the provider-neutral call.
- Registering the behaviour again when one registration has settings throws ([0027](0027-startup-checks.md)).

## Consequences

- One registration line shows the behaviour's position and settings.
- Every SQL provider already depends on `Microsoft.EntityFrameworkCore.Relational`, so it adds nothing for them. A non-relational provider works with the defaults, and fails loudly with an isolation level.
- A setting added later is a new property, which isn't a breaking change.
