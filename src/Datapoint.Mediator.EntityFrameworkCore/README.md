# Datapoint.Mediator.EntityFrameworkCore

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that runs each command in an Entity Framework Core transaction, and saves its changes once, after the handler succeeds. Handlers don't call `SaveChangesAsync`. Register it with [Datapoint.Mediator.EntityFrameworkCore.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.EntityFrameworkCore.DependencyInjection).

```csharp
[NonTransactional]   // calls an external system: no transaction, and the handler saves itself
public sealed record SendInvoiceEmail(Guid InvoiceId) : ICommand;
```

> **Retries:** with a retrying execution strategy, such as `EnableRetryOnFailure`, a transient failure runs the **whole command again**, handler and message handlers included. Database changes are rolled back first, but external calls, such as HTTP requests or emails, happen again. Make them idempotent, or move them to a `[NonTransactional]` command.

Optionally, set isolation levels, run queries in read-only transactions, and prepare each transaction with an `ITransactionInitializer<TDbContext>`, for example for PostgreSQL row-level security.

**Documentation:** [Transactions](https://github.com/datapoint-software/mediator/blob/main/docs/transactions.md)
