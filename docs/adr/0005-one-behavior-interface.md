# 5. One `IBehavior` with an overload per kind

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

Cross-cutting concerns, such as authorization, validation, transactions and auditing, apply to some kinds of dispatch and not others: transactions only apply to commands, and authorization doesn't apply to messages. Libraries often use a generic `IPipelineBehavior<TRequest, TResponse>`, which needs one closed generic registration per request type, or open generics resolved at runtime.

## Decision

There is one non-generic `IBehavior` interface with a `HandleAsync` overload for each kind: command, command with result, query, stream query and message. Every overload has a default implementation that calls `next`, so a behaviour only implements the overloads it needs. Behaviours run in registration order, the first one outermost.

## Consequences

- One registration per behaviour, resolved once per dispatch as `IEnumerable<IBehavior>`.
- A behaviour can't accidentally apply to a kind it doesn't override: `TransactionBehavior` simply doesn't override queries.
- A new kind of dispatch would add an overload with a default implementation, which isn't breaking for existing behaviours.
- Behaviours inspect the action's type at runtime, for attributes such as `[NonTransactional]` or `[AllowAnonymousAccess]`, and cache what they find per type.
