# Transactions

`Datapoint.Mediator.EntityFrameworkCore` runs each command in an Entity Framework Core transaction, and saves its changes once, after the handler succeeds.

```bash
dotnet add package Datapoint.Mediator.EntityFrameworkCore.DependencyInjection --prerelease
```

```csharp
services.AddDbContext<InvoicingDbContext>(o => o.UseSqlServer(connectionString));
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()
    .AddValidationBehavior()
    .AddTransactionBehavior<InvoicingDbContext>());   // last: only allowed, valid commands open a transaction
```

## Handlers don't save

For each command, the behaviour begins a transaction, runs the handler, calls `SaveChangesAsync` and commits. If anything fails, it rolls back and nothing is saved. Handlers just change entities:

```csharp
public sealed class CreateInvoiceHandler(InvoicingDbContext db) : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default)
    {
        // Generated here, because database-generated keys are only known after the save.
        var invoice = new Invoice { Id = Guid.CreateVersion7(), Customer = command.Customer, Amount = command.Amount };
        db.Invoices.Add(invoice);
        return new(invoice.Id);   // no SaveChangesAsync
    }
}
```

The behaviour is scoped, so it uses the same `DbContext` instance as the handlers in the current scope.

## What is affected

| Request | Transaction |
|---|---|
| Command | yes, unless it's marked `[NonTransactional]` |
| Command sent from another command's handler | joins the outer transaction, and is saved and committed with it, even if it's marked `[NonTransactional]` |
| Message published from a command's handler | runs inside the command's transaction |
| Query, stream query | no |

### Messages run inside the transaction

When a handler publishes a message, its handlers run immediately, inside the same transaction. A message handler that writes to the database is committed or rolled back together with the command. That's what the samples' audit entry relies on.

A message handler that calls the outside world, such as sending an email, *isn't* rolled back: the email is sent even if the command later fails. See [Side effects](#side-effects-and-retries).

## `[NonTransactional]`

Mark commands that shouldn't run in a transaction, typically because they call external systems, or manage their own transactions:

```csharp
[NonTransactional]
public sealed record SendInvoiceEmail(Guid InvoiceId) : ICommand;
```

The handler must call `SaveChangesAsync` itself, if it changes anything. The attribute isn't inherited, and has no effect when the command is sent from inside another command's transaction.

## Side effects and retries

EF Core can retry transient failures, such as deadlocks or dropped connections, when you turn it on, for example with `UseSqlServer(cs, sql => sql.EnableRetryOnFailure())`. With retries on, a transient failure runs the **whole command again**: handler and message handlers included, after rolling back and clearing the change tracker.

| What the command does | After a retry |
|---|---|
| Changes entities through the same `DbContext` | happens once: the failed attempt was rolled back |
| Sends an email, calls an HTTP API, posts to a queue | happens **again** |

So any external call made inside a transactional command, in its handler or in a message handler, must be safe to repeat:
- make it **idempotent**, for example by sending an idempotency key that the other system uses to ignore duplicates, as payment APIs support;
- or move it to a `[NonTransactional]` command sent from the host, which saves first and then calls out:

```csharp
[NonTransactional]
public sealed record ChargeInvoice(Guid InvoiceId) : ICommand;

// In its handler:
invoice.Status = InvoiceStatus.Charging;
await db.SaveChangesAsync(ct);                             // commit first
await payments.ChargeAsync(invoice, idempotencyKey: invoice.Id, ct);   // then call out
```

In the rare case where the connection drops while committing, the commit may have succeeded on the server, and the retry saves the same changes again. A unique constraint, such as on an idempotency key, turns that into an error instead of a duplicate.

These trade-offs are deliberate: see [ADR 0009](adr/0009-messages-run-in-the-callers-transaction.md) and [ADR 0010](adr/0010-transaction-retries-and-nesting.md).
