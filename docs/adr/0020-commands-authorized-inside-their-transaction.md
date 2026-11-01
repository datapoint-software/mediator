# 20. Commands and queries can be authorized inside their transaction

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

The recommended order ran authorization first, then validation, then the transaction, so a command was authorized against one snapshot of the database and ran in another. When access can change between the two, such as a share revoked or a label changed, that's a time-of-check/time-of-use gap. Authorization must also stay before validation, so validation errors don't reveal rules to users who may not run an action.

Two designs were considered:
1. A second authorization behaviour inside the transaction, keeping the first one outside. Every command would be authorized, and observed, twice.
2. Registering the transaction behaviour before authorization. No new behaviour is needed, and each request is authorized once. But one misordered registration silently reopens the gap, and requests from callers who aren't signed in begin a transaction, and run its initializers ([0022](0022-transaction-initializers.md)), before they're refused.

## Decision

- Authorization runs inside the transaction **by behaviour order**: authentication, then the transaction, then authorization, then validation. The transaction behaviour only wraps commands, and queries when they're switched on ([0024](0024-read-only-query-transactions.md)), so other requests keep the order authorization, then validation.
- `AuthenticationBehavior` (`AddAuthenticationBehavior()`), registered first, refuses requests without an authenticated user before the transaction begins. It's told to observers as `AuthenticationRequired`. `AuthorizationBehavior` still checks authentication too.
- `AddAuthorizationBehavior(a => a.RequireTransaction = …)` makes the order enforceable: `Commands` or `CommandsAndQueries` (stream queries included).
  - At runtime, authorizing a non-anonymous action of a required kind when `ITransactionContext` ([0026](0026-transaction-context-in-abstractions.md)) isn't registered or isn't active throws `InvalidOperationException`, before any authorizer runs. For stream queries, that's at first enumeration.
  - At startup, a check ([0027](0027-startup-checks.md)) refuses authorized actions of a required kind marked `[NonTransactional]`. It recognises the attribute by its full name, `Datapoint.Mediator.EntityFrameworkCore.NonTransactionalAttribute`, so the authorization package doesn't depend on EF Core; a test using both packages fails if the attribute is renamed.
- The order authorization, validation, transaction stays supported, for applications without this need.

## Consequences

- Authorizers and validators read the same transaction as the handler, prepared by the initializers, such as a row-level security setting. A retried transaction authorizes and observes again ([0024](0024-read-only-query-transactions.md)).
- Being inside the transaction only closes the gap at a strong enough isolation level, or with row locks ([0021](0021-transaction-behavior-options.md)).
- A denial throws inside the transaction, which rolls back. An observer that must keep denials writes them through its own connection.
- Authorized requests that are refused still begin a transaction. Authentication first limits that to signed-in users.
- Authorizers must not change tracked entities, or the changes are saved with the command.
- `[NonTransactional]` actions can't be authorized inside a transaction. With `RequireTransaction`, they must be anonymous, or the requirement lowered.
