# 27. Startup checks, and behaviours with settings are registered once

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Some behaviours need settings in their constructor, such as a transaction's isolation level, so `MediatorOptions.AddBehavior` gained an overload that takes a factory. Behaviour types were registered once, and a second registration was ignored. With settings, that means a second `AddTransactionBehavior<T>(…)` call with different settings would be silently ignored, and an application could run with settings it didn't intend. [0014](0014-one-principal-accessor.md) refuses a second principal accessor for the same reason.

Some configuration mistakes involve several packages. For example, requiring authorization inside a transaction while an authorized action is marked `[NonTransactional]` can only be found by looking at every registered action. Finding it at the first request means finding it in production for a rarely used action. Invalid action names are already refused in `AddMediator` ([0011](0011-named-actions.md)).

## Decision

- `MediatorOptions.AddBehavior<TBehavior>(Func<IServiceProvider, TBehavior>)` registers a behaviour created by a factory.
- A behaviour type registered with a factory can only be registered once. Registering it again, with or without a factory, and in the same or a later `AddMediator` call, throws `InvalidOperationException`. Registering a type twice without a factory still has no effect, so methods without settings stay idempotent.
- `MediatorOptions.AddStartupCheck(Action<MediatorStartupContext>)` registers a check. Checks run at the end of every `AddMediator` call, over every command, query and stream query registered so far (`MediatorStartupContext.RequestTypes`), including requests added by later calls. A check reports a mistake by throwing.

## Consequences

- Conflicting settings fail at startup instead of being ignored.
- Packages can validate their configuration against every registered request, without the core knowing about them.
- Checks run once per `AddMediator` call, so they should be cheap: they inspect types, which is reflection at startup, not per dispatch.
- A check registered by an early call sees requests from later calls, but a check registered by a later call also sees earlier requests: the order of `AddMediator` calls doesn't matter.
