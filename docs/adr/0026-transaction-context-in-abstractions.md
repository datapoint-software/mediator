# 26. The transaction state is a contract in Abstractions

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Two needs depend on knowing whether, and how, the current request runs in a transaction:
- authorization can be required to run inside the transaction ([0020](0020-authorization-inside-the-transaction.md)), so the authorization behaviour must know whether one is active;
- audit observers that write through their own connection record decisions for attempts that may be rolled back and retried ([0010](0010-transaction-retries-and-nesting.md)), so they need the attempt number and an identifier for each attempt.

The authorization package must not depend on Entity Framework Core ([0004](0004-package-split.md)). Matching types by name, as adapters do for exceptions ([0013](0013-adapters-match-authorization-exceptions-by-name.md)), works for a type check, but not for reading state.

A scoped service was considered, but a command and the queries it sends share a scope, so a nested request would overwrite the outer request's state. Parallel dispatches would see each other's state too.

## Decision

- `ITransactionContext` lives in `Datapoint.Mediator.Abstractions`, with `IsActive`, `IsReadOnly`, `IsJoined`, `Attempt` and `AttemptId`.
- The transaction behaviour implements and registers it. Its state flows with the asynchronous call, like the authorization decision ([0025](0025-decisions-bound-to-their-request.md)): each request sees its own state, a nested request's state is gone when it returns, and parallel dispatches don't share it.
- With no transaction, `IsActive` is `false`, `Attempt` is 0 and `AttemptId` is `Guid.Empty`.

## Consequences

- Authorization, observers and handlers read the transaction state without depending on EF Core.
- Another transaction behaviour, for another data access library, can implement the same contract.
- Abstractions gains a contract that only transaction behaviours implement. It has no implementation of its own, so applications without a transaction behaviour don't register it.
