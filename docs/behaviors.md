# Behaviours

A behaviour wraps the handling of requests, to add a cross-cutting concern once instead of in every handler.

## Built-in behaviours

| Behaviour | Package | Applies to | Guide |
|---|---|---|---|
| Authorization | `Datapoint.Mediator.Authorization` | commands, queries, stream queries | [Authorization](authorization.md) |
| Validation | `Datapoint.Mediator.FluentValidation` | commands, queries, stream queries | [Validation](validation.md) |
| Transactions | `Datapoint.Mediator.EntityFrameworkCore` | commands | [Transactions](transactions.md) |

## Order

Behaviours run in the order they're added, the first one outermost. The recommended order is:

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
