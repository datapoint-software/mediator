# Datapoint.Mediator.DependencyInjection

Registers [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator), your handlers and behaviours with `Microsoft.Extensions.DependencyInjection`.

```csharp
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddBehavior<LoggingBehavior>());
```

`IMediator`, handlers and behaviours are scoped by default. A second handler for the same command or query, or an invalid action name, throws when `AddMediator` runs.

**Documentation:** [Dependency injection](https://github.com/datapoint-software/mediator/blob/main/docs/dependency-injection.md) · [Manual](https://github.com/datapoint-software/mediator/blob/main/docs/README.md)
