# Datapoint.Mediator

A fast Mediator pattern implementation for commands, queries and messages, with behaviour pipelines.

Most applications register it with [Datapoint.Mediator.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.DependencyInjection) rather than referencing this package directly.

```csharp
var id = await mediator.SendAsync(new CreateUser("Ana"), ct);
var user = await mediator.QueryAsync(new GetUser(id), ct);
await mediator.PublishAsync(new UserCreated(id), ct);
```

- Dispatch is cached per action type after its first use, and adds no allocations when no behaviours are registered.
- Behaviours run in registration order, with the first one outermost.
- A message's handlers run one after another, in registration order, and publishing stops at the first exception.
- Handlers and behaviours are resolved from the `IServiceProvider` given to `Mediator`, so create it from the current scope.
