# 6. Cached generic dispatchers, allocation-free without behaviours

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

`IMediator.SendAsync(ICommand)` receives a command typed as an interface, but has to resolve `ICommandHandler<TCommand>` for its concrete type. Reflection or `dynamic` on every call is slow and allocates. The mediator sits on every request's path, so its overhead should be close to a direct handler call.

## Decision

- For each concrete type, the mediator creates a generic dispatcher once, such as `CommandDispatcher<TCommand>`, and caches it in a static `ConcurrentDictionary<Type, …>`. Later dispatches are a dictionary lookup and a virtual call, with no reflection.
- When no behaviours are registered, the dispatcher calls the handler directly. The behaviour pipeline, which needs closures, is built in a separate method, so those closures are only allocated when there are behaviours.
- `benchmarks/Datapoint.Mediator.Benchmarks` measures this with BenchmarkDotNet.

## Consequences

- Dispatch without behaviours allocates 0 bytes, and with behaviours it allocates only the pipeline's closures.
- The caches grow with the number of distinct action types, which is bounded by the application's code.
- Changes to dispatch must be measured with the benchmarks, so a refactoring doesn't silently add allocations.
