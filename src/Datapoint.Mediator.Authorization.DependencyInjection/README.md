# Datapoint.Mediator.Authorization.DependencyInjection

Registers the [Datapoint.Mediator.Authorization](https://www.nuget.org/packages/Datapoint.Mediator.Authorization) behaviour and your authorizers.

```csharp
services.AddScoped<IPrincipalAccessor, HttpPrincipalAccessor>();   // one per host
services.AddAuthorizersFromAssemblyContaining<DeleteInvoiceAuthorizer>();
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<DeleteInvoiceHandler>();
    options.AddAuthorizationBehavior();   // before validation
    options.AddValidationBehavior();
});
```

- The behaviour is always registered as scoped, so it resolves the current user and authorizers from the current scope.
- `AddAuthorizersFromAssembly` registers every concrete authorizer class, as scoped by default. An action can have several authorizers, and all of them must allow it.
