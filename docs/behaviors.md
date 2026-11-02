# Behaviours

A behaviour wraps the handling of requests, to add a cross-cutting concern once instead of in every handler.

## Built-in behaviours

| Behaviour | Package | Applies to | Guide |
|---|---|---|---|
| Authorization | `Datapoint.Mediator.Authorization` | commands, queries, stream queries | [Authorization](authorization.md) |
| Validation | `Datapoint.Mediator.FluentValidation` | commands, queries, stream queries | [Validation](validation.md) |
| Transactions | `Datapoint.Mediator.EntityFrameworkCore` | commands | [Transactions](transactions.md) |

## Order

Behaviours run in the order they're added, the first one outermost. There are two recommended orders.

**Usually**, refuse first and keep the transaction short:

```csharp
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddBehavior<ExceptionHandlingBehavior>()          // your own: turns failures into results, if you use one
    .AddAuthorizationBehavior()                        // refuse before anything reveals information
    .AddValidationBehavior()                           // refuse invalid input before a transaction opens
    .AddTransactionBehavior<InvoicingDbContext>());    // innermost: wraps only the handler
```

- **Authorization before validation,** so a user who may not run an action can't learn its validation rules from the errors.
- **Validation before the transaction,** so invalid requests never open one.
- **The transaction innermost,** so it's as short as possible.

**When access can change between the check and the handler,** such as a share revoked while a command runs, authorize inside the transaction, so the rules are checked against the same data the handler reads:

```csharp
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<DownloadItemHandler>()
    .AddAuthenticationBehavior()                                          // refuse anonymous callers before BEGIN
    .AddTransactionBehavior<AppDbContext>(t =>
    {
        t.IsolationLevel = IsolationLevel.ReadCommitted;                  // plus your own row locks
        t.Queries = QueryTransactions.ReadOnly;                           // queries in transactions too
        t.QueryIsolationLevel = IsolationLevel.RepeatableRead;
        t.ReadOnlyStatement = "SET TRANSACTION READ ONLY";
        t.ForeignTransactions = ForeignTransactions.Throw;
    })
    .AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.CommandsAndQueries)
    .AddValidationBehavior());                                            // still after authorization
```

| Request | Pipeline |
|---|---|
| Command | authentication → BEGIN → initializers → authorization → validation → handler → save → commit |
| Query | authentication → BEGIN → read-only statement → initializers → authorization → validation → handler → rollback |
| Stream query | the same as a query, at first enumeration; rolled back when the enumeration ends |
| Retry | the whole attempt again, authorization and its observers included |

`RequireTransaction` makes the order a rule: if the transaction behaviour ends up after authorization, requests fail instead of being silently authorized outside the transaction. See [Authorizing inside the transaction](authorization.md#authorizing-inside-the-transaction) and [Transactions](transactions.md).

## Writing a behaviour

Implement `IBehavior`, and override only the kinds of request you care about. Every method has a default that just calls `next`:

```csharp
public sealed class SlowRequestLoggingBehavior(ILogger<SlowRequestLoggingBehavior> logger) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        await next(ct);
        LogIfSlow(command, started);
    }

    public async ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command, Func<CancellationToken, ValueTask<TResult>> next, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await next(ct);
        LogIfSlow(command, started);
        return result;
    }

    private void LogIfSlow(object request, long started)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        if (elapsed > TimeSpan.FromSeconds(1))
        {
            logger.LogWarning("{Request} took {Elapsed}", request.GetType().Name, elapsed);
        }
    }
}
```

```csharp
o.AddBehavior<SlowRequestLoggingBehavior>();
```

A behaviour that needs settings in its constructor can be registered with a factory, which gets the current scope's services:

```csharp
o.AddBehavior(sp => new SlowRequestLoggingBehavior(sp.GetRequiredService<ILogger<SlowRequestLoggingBehavior>>(), TimeSpan.FromSeconds(2)));
```

Registering the same behaviour type twice without a factory has no effect. A behaviour registered with a factory can only be registered once: registering its type again, with or without a factory, makes `AddMediator` throw `InvalidOperationException`, so its settings are never silently ignored.

### Startup checks

A behaviour's registration method can add a check that runs in `AddMediator`, so a configuration mistake fails at startup instead of at the first request:

```csharp
public static MediatorOptions AddAuditBehavior(this MediatorOptions options) => options
    .AddBehavior<AuditBehavior>()
    .AddStartupCheck(context =>
    {
        foreach (var type in context.RequestTypes)   // every command, query and stream query with a handler
        {
            if (type.IsDefined(typeof(AuditedAttribute)) && !type.IsSealed)
            {
                throw new InvalidOperationException($"'{type}' is audited, so it must be sealed.");
            }
        }
    });
```

Checks run at the end of every `AddMediator` call, over every request registered so far, including those from later calls.

| Overload | Called for |
|---|---|
| `HandleAsync(ICommand, …)` | commands without a result |
| `HandleAsync<TResult>(ICommand<TResult>, …)` | commands with a result |
| `HandleAsync<TResult>(IQuery<TResult>, …)` | queries |
| `HandleAsync<TResult>(IStreamQuery<TResult>, …)` | stream queries; returns `IAsyncEnumerable<TResult>` |
| `HandleAsync(IMessage, …)` | messages, once per publish, around all of its handlers |

Guidelines:
- Call `next` at most once, and pass `ct` on.
- To refuse a request, throw an exception instead of calling `next`.
- Don't log requests or results as they are: they can contain personal data. Log their type name.
- Behaviours are resolved per dispatch. Keep their constructors cheap, and cache reflection in `static` fields, per type.
- To read an attribute on the request type, such as your own `[Audited]`, cache the answer per type in a `static ConcurrentDictionary<Type, bool>`.

## Performance

With no behaviours registered, dispatch allocates nothing. Each behaviour adds a small allocation per dispatch for the pipeline.
