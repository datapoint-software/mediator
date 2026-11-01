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
- A command sent from another command's handler joins the outer transaction, even if it is marked `[NonTransactional]`.
- Messages published from a command's handler run inside its transaction.

> **Retries:** with a retrying execution strategy, such as `EnableRetryOnFailure`, a transient failure runs the **whole command again, handler and message handlers included**. Its database changes are rolled back first, but external calls, such as HTTP requests, emails or queue messages, are not, so they happen again. Make those calls idempotent, for example with an idempotency key, or move them to a `[NonTransactional]` command sent from the host, which saves before calling out.
>
> In the rare case where the connection drops while committing, the commit may have succeeded on the server, and the retry saves the same changes again. A unique constraint, such as on an idempotency key, turns that into an error instead of a duplicate.
