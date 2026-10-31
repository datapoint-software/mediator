# Datapoint.Mediator

A fast Mediator pattern implementation for commands, queries and messages, with behaviour pipelines.

Most applications register it with [Datapoint.Mediator.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.DependencyInjection) rather than referencing this package directly.

```csharp
var id = await mediator.SendAsync(new CreateUser("Ana"), ct);
var user = await mediator.QueryAsync(new GetUser(id), ct);
await mediator.PublishAsync(new UserCreated(id), ct);

await foreach (var user in mediator.StreamAsync(new ListUsers(), ct))
{
    // Results arrive as the handler produces them.
}
```

- Dispatch is cached per action type after its first use, and adds no allocations when no behaviours are registered.
- Behaviours run in registration order, with the first one outermost.
- A message's handlers run one after another, in registration order, and publishing stops at the first exception.
- Handlers and behaviours are resolved from the `IServiceProvider` given to `Mediator`, so create it from the current scope.

## Tracing and metrics

Every dispatch produces a span (for example `send CreateUser`) and a `datapoint.mediator.dispatch.duration` measurement, in seconds. To collect them with OpenTelemetry:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MediatorTelemetry.SourceName))
    .WithMetrics(metrics => metrics.AddMeter(MediatorTelemetry.MeterName));
```

Spans and measurements are tagged with the operation (`send`, `query`, `stream` or `publish`), the action's type name and, on failure, `error.type`. Exception messages are never recorded, because they can contain user input or personal data. When nothing listens, the mediator adds no overhead.
