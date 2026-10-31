# Datapoint.Mediator.EntityFrameworkCore.DependencyInjection

Registers the [Datapoint.Mediator.EntityFrameworkCore](https://www.nuget.org/packages/Datapoint.Mediator.EntityFrameworkCore) transaction behaviour.

```csharp
services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>();
    options.AddAuthorizationBehavior();
    options.AddValidationBehavior();
    options.AddTransactionBehavior<AppDbContext>(); // last: only allowed, valid commands open a transaction
});
```

The behaviour is always registered as scoped, so it uses the same `AppDbContext` as the handlers in the current scope.
