# Datapoint.Mediator.EntityFrameworkCore

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that runs each command in an Entity Framework Core transaction, and saves its changes once, after the handler succeeds. If the handler or the save fails, the transaction is rolled back and nothing is saved.

Register it with [Datapoint.Mediator.EntityFrameworkCore.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.EntityFrameworkCore.DependencyInjection).

```csharp
public sealed class CreateInvoiceHandler(AppDbContext db) : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default)
    {
        // The key is generated here, because database-generated keys are only known after the save.
        var invoice = new Invoice(Guid.NewGuid(), command.Amount);
        db.Invoices.Add(invoice);
        return new(invoice.Id); // No SaveChangesAsync: the behaviour saves and commits.
    }
}

[NonTransactional] // Sends an email, which can't be rolled back or safely repeated.
public sealed record SendInvoiceEmail(int InvoiceId) : ICommand;
```

- Every command runs in a transaction, unless it is marked `[NonTransactional]`. Queries, stream queries and messages are not affected.
- A command sent from another command's handler joins the outer transaction.
- Messages published from a command's handler run inside its transaction.

> **Retries:** with a retrying execution strategy, such as `EnableRetryOnFailure`, a transient failure runs the **whole command again, handler included**. Make handlers safe to repeat, and keep side effects such as emails in `[NonTransactional]` commands.
