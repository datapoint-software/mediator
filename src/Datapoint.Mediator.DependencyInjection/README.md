# Datapoint.Mediator.DependencyInjection

Registers [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator), its handlers and behaviours with `Microsoft.Extensions.DependencyInjection`.

```csharp
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<CreateUserHandler>();
    options.AddBehavior<LoggingBehavior>();
});
```

- `IMediator` is registered as a scoped service.
- Handlers can be added one by one with `AddHandler<T>()`, or found by scanning an assembly with `AddHandlersFromAssembly(...)`. Scanning never registers behaviours, because their order matters.
- Behaviours run in the order they are added with `AddBehavior<T>()`.
- Handlers and behaviours are scoped by default; change it with `options.DefaultLifetime` or per registration.
- Registering a second handler for the same command or query throws when `AddMediator` runs, so the mistake is found at startup.
