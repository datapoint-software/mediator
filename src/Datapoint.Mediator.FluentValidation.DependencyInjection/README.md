# Datapoint.Mediator.FluentValidation.DependencyInjection

Registers the [Datapoint.Mediator.FluentValidation](https://www.nuget.org/packages/Datapoint.Mediator.FluentValidation) behaviour.

```csharp
services.AddValidatorsFromAssemblyContaining<CreateUserValidator>(); // FluentValidation's own registration
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<CreateUserHandler>();
    options.AddValidationBehavior();
});
```

- The behaviour is always registered as scoped, so it resolves validators from the current scope.
- It runs at the position it's added in, relative to other behaviours. Add it after any behaviour that should handle validation failures, such as exception handling.
