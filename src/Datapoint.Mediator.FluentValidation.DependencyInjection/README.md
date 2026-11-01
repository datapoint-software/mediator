# Datapoint.Mediator.FluentValidation.DependencyInjection

Registers the [Datapoint.Mediator.FluentValidation](https://www.nuget.org/packages/Datapoint.Mediator.FluentValidation) behaviour.

```csharp
services.AddValidatorsFromAssemblyContaining<CreateInvoiceValidator>();   // FluentValidation's own registration
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()
    .AddValidationBehavior());
```

**Documentation:** [Validation](https://github.com/datapoint-software/mediator/blob/main/docs/validation.md) · [Behaviour order](https://github.com/datapoint-software/mediator/blob/main/docs/behaviors.md#order)
