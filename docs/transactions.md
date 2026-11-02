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
| Query, stream query | no, unless [query transactions](#read-only-query-transactions) are on |
| Query or stream query sent from a command's handler | runs inside the command's transaction |
| Request sent while the application has its own transaction open | joins it, and [isn't saved](#who-saves-and-commits) |

### Messages run inside the transaction

When a handler publishes a message, its handlers run immediately, inside the same transaction. A message handler that writes to the database is committed or rolled back together with the command. That's what the samples' audit entry relies on.

A message handler that calls the outside world, such as sending an email, *isn't* rolled back: the email is sent even if the command later fails. See [Side effects](#side-effects-and-retries).

## `[NonTransactional]`

Mark commands that shouldn't run in a transaction, typically because they call external systems, or manage their own transactions. With [query transactions](#read-only-query-transactions) on, it also excludes a query or stream query:

```csharp
[NonTransactional]
public sealed record SendInvoiceEmail(Guid InvoiceId) : ICommand;
```

The handler must call `SaveChangesAsync` itself, if it changes anything. The attribute isn't inherited, and has no effect when the request is sent from inside another request's transaction.

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

These trade-offs are deliberate: see [ADR 0009](adr/0009-messages-run-in-the-callers-transaction.md) and [ADR 0024](adr/0024-read-only-query-transactions.md).

## Settings

Pass settings when you register the behaviour:

```csharp
.AddTransactionBehavior<AppDbContext>(t =>
{
    t.IsolationLevel = IsolationLevel.ReadCommitted;          // commands; default: the database's
    t.Queries = QueryTransactions.ReadOnly;                   // queries too; default: None
    t.QueryIsolationLevel = IsolationLevel.RepeatableRead;    // one snapshot per query on PostgreSQL
    t.ReadOnlyStatement = "SET TRANSACTION READ ONLY";        // run first in every read-only transaction
    t.ForeignTransactions = ForeignTransactions.Throw;        // refuse transactions the mediator didn't begin
})
```

- An isolation level other than `Unspecified` needs a relational provider, such as SQL Server or PostgreSQL.
- An undefined value throws when you register. Don't keep or change the settings object afterwards.
- The behaviour can only be registered once with settings: registering it again, with or without settings, makes `AddMediator` throw, so settings are never silently ignored.

Being inside a transaction doesn't make every read consistent: at `READ COMMITTED`, each statement sees the latest committed data. Choose the level, or take row locks such as `SELECT … FOR SHARE`, according to what must not change between a check and the write that depends on it.

## Preparing each transaction

An `ITransactionInitializer<TDbContext>` runs right after the transaction begins, before anything else runs in it: authorization, validation and the handler. For example, for PostgreSQL row-level security with a transaction-local setting:

```csharp
public sealed class TenantInitializer(IPrincipalAccessor principals) : ITransactionInitializer<AppDbContext>
{
    public async ValueTask InitializeAsync(AppDbContext db, TransactionInitialization transaction, CancellationToken ct = default)
    {
        if (transaction.IsJoined)
        {
            return;   // already prepared by the outer request; check it matches here if you need to
        }

        // From the authenticated user, never from transaction.Request alone.
        var tenantId = principals.Principal?.FindFirstValue("tenant_id");

        if (tenantId is not null)
        {
            await db.Database.ExecuteSqlAsync($"SELECT set_config('app.tenant_id', {tenantId}, true)", ct);
        }
    }
}
```

```csharp
services.AddTransactionInitializer<AppDbContext, TenantInitializer>();
```

- Initializers run in registration order, after `ReadOnlyStatement`, and **again on every retry**.
- They also run when a request joins an outer request's transaction, with `IsJoined` set and nothing to prepare: use it to check that the nested request matches, such as the same tenant.
- If one throws, the request fails and the transaction is rolled back.
- They run before authorization, so they can run for callers that aren't signed in, unless [authentication](authorization.md#authentication-before-the-transaction) runs first. Write row-level security policies so that a missing setting matches no rows.

> **Security:** in a multi-tenant app where one user belongs to several tenants, the tenant comes from the request. Check the user's membership of that tenant **before** `set_config`. A tenant id taken from the request without that check lets any user read another tenant's rows.

## Read-only query transactions

With `Queries = QueryTransactions.ReadOnly`, queries and stream queries run in transactions too, prepared the same way, and **always rolled back**: nothing a query changes is saved, and the change tracker forgets it.

| Request | Transaction |
|---|---|
| Query | begins, runs `ReadOnlyStatement` and the initializers, runs the pipeline, rolls back. Transient failures retry the whole query |
| Stream query | begins when the results are first enumerated, and is rolled back when the enumeration completes, fails or is stopped |
| Stream query, with a retrying execution strategy | refused with `InvalidOperationException`: results already returned can't be retried. Return a list from a query instead, or use a context without retries |
| `[NonTransactional]` query or stream query | not wrapped |

- On PostgreSQL, set `ReadOnlyStatement = "SET TRANSACTION READ ONLY"`, so writes fail immediately. It must be the first statement in the transaction, which is why the behaviour runs it before the initializers. SQL Server has no read-only transactions: there, rolling back is the only protection.
- Every query costs a `BEGIN` and a `ROLLBACK`.
- A stream query holds a connection, and a snapshot, for as long as it's enumerated. On PostgreSQL, a long transaction also holds back vacuum. Set `idle_in_transaction_session_timeout` on the database role, so a stream abandoned by a slow consumer can't hold them forever.

## The current transaction

`ITransactionContext` tells behaviours, handlers and observers about the current request's transaction. Register it once:

```csharp
services.AddTransactionContext();
```

| Property | Meaning |
|---|---|
| `IsActive` | the request runs in a transaction begun by the mediator, its own or a joined one |
| `IsReadOnly` | it's a read-only query transaction |
| `IsJoined` | the request joined an outer request's transaction |
| `Attempt` | 1, then 2, 3… when the transaction is retried |
| `AttemptId` | new for every attempt |

The state flows with the call: a nested request sees its own state, the outer request sees its own again afterwards, and parallel dispatches don't share it. An audit written through its own connection can record `AttemptId`, and match it with what was committed.

## Who saves and commits

**Whoever begins a transaction saves and commits it.**

- The behaviour saves and commits the transactions it begins. A request sent from a handler joins that transaction, and is saved and committed with the outer request.
- If your code begins a transaction itself and then sends a request, the request joins it, and the behaviour doesn't save, commit, prepare or retry anything: your code must call `SaveChangesAsync` and commit.

You don't need to begin transactions yourself: isolation levels, preparation and read-only queries all come from the behaviour. To make sure nobody does, set `ForeignTransactions = ForeignTransactions.Throw`: a request then fails when it finds a transaction the mediator didn't begin.

See [ADR 0021](adr/0021-transaction-behavior-options.md), [ADR 0022](adr/0022-transaction-initializers.md) and [ADR 0023](adr/0023-whoever-begins-a-transaction-commits-it.md).
