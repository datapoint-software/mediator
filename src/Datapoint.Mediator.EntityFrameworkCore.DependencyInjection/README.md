# Datapoint.Mediator.EntityFrameworkCore.DependencyInjection

Registers the [Datapoint.Mediator.EntityFrameworkCore](https://www.nuget.org/packages/Datapoint.Mediator.EntityFrameworkCore) transaction behaviour.

```csharp
services.AddDbContext<InvoicingDbContext>(o => o.UseSqlServer(connectionString));
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()
    .AddValidationBehavior()
    .AddTransactionBehavior<InvoicingDbContext>());   // last: only allowed, valid commands open a transaction
```

**Documentation:** [Transactions](https://github.com/datapoint-software/mediator/blob/main/docs/transactions.md) · [Behaviour order](https://github.com/datapoint-software/mediator/blob/main/docs/behaviors.md#order)
