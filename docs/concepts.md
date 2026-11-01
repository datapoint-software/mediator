# Concepts

## The mediator pattern

Without a mediator, an endpoint calls a service, which calls other services. Every caller depends on the classes that do the work. With a mediator, callers describe *what* they want as a small object, and the mediator finds the one class that handles it:

```csharp
var id = await mediator.SendAsync(new CreateInvoice("Ana", 100m), ct);
```

This keeps callers thin, gives every operation a name and a type, and lets cross-cutting concerns, such as authorization, validation and transactions, wrap every operation in one place: the [pipeline](#the-pipeline).

## Four kinds of request

| Kind | Declare it as | Handled by | Send it with | Typical use |
|---|---|---|---|---|
| Command | `ICommand` or `ICommand<TResult>` | exactly one `ICommandHandler<TCommand>` or `ICommandHandler<TCommand, TResult>` | `SendAsync` | change state: create, update, cancel |
| Query | `IQuery<TResult>` | exactly one `IQueryHandler<TQuery, TResult>` | `QueryAsync` | read state |
| Stream query | `IStreamQuery<TResult>` | exactly one `IStreamQueryHandler<TQuery, TResult>` | `StreamAsync` | read many results, as they arrive |
| Message | `IMessage` | any number of `IMessageHandler<TMessage>` | `PublishAsync` | announce something that happened |

Declare requests as `sealed record`s: they're immutable, compare by value, and serialize cleanly to and from JSON.

### Commands

```csharp
public sealed record CancelInvoice(Guid Id, string Reason) : ICommand;
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;
```

A command may return a result, such as a new id, but its purpose is the change. Keep queries for reading.

### Queries and stream queries

```csharp
public sealed record GetInvoice(Guid Id) : IQuery<InvoiceView?>;
public sealed record ListInvoices(int Max = 50) : IStreamQuery<InvoiceView>;
```

A stream query's handler returns an `IAsyncEnumerable<T>`. The caller receives results while the handler is still producing them, which suits large result sets and streaming hosts:

```csharp
await foreach (var invoice in mediator.StreamAsync(new ListInvoices(), ct))
{
    Console.WriteLine(invoice.Customer);
}
```

The handler is resolved when `StreamAsync` is called, but it only runs as the results are enumerated.

### Messages

```csharp
public sealed record InvoiceCancelled(Guid Id, string Reason) : IMessage;

await mediator.PublishAsync(new InvoiceCancelled(invoice.Id, command.Reason), ct);
```

- A message can have no handlers, or many. They run one after another, in registration order, and the first exception stops the rest.
- `await PublishAsync` completes once every handler has run.
- Inside a transactional command, message handlers run inside its transaction. See [Transactions](transactions.md).
- Messages are **not authorized**. Publish them from your own code, typically from a command's handler, and never build one directly from user input. See [Authorization](authorization.md#messages).

## Handlers

A handler is a class that implements one handler interface per request it handles. It receives its dependencies through its constructor, like any other service:

```csharp
public sealed class GetInvoiceHandler(InvoicingDbContext db) : IQueryHandler<GetInvoice, InvoiceView?>
{
    public async ValueTask<InvoiceView?> HandleAsync(GetInvoice query, CancellationToken ct = default) => ...;
}
```

- Every method returns `ValueTask`, so a handler that completes synchronously costs no allocation. Await the result once, and never twice.
- Pass `ct` on to everything asynchronous, so a cancelled request stops its work.
- Registering a second handler for the same command or query fails at startup. See [Dependency injection](dependency-injection.md).

## The pipeline

Between `IMediator` and the handler, each request goes through the registered **behaviours**, in registration order:

```
SendAsync ─▶ Authorization ─▶ Validation ─▶ Transaction ─▶ handler
                                                             │
SendAsync ◀──────────────────────────────────────────────────┘
```

Each behaviour can run code before and after the rest of the pipeline, or stop it by throwing. That's how authorization refuses a request before validation runs, and how the transaction behaviour commits only after the handler succeeds. See [Behaviours](behaviors.md).

## Actions

Commands and queries marked with `[Action("invoices.create")]` can also be dispatched **by name**, with JSON arguments. That's what lets one business layer be offered as HTTP endpoints, CLI commands and MCP tools. See [Actions](actions.md).

## Where things live

| Layer | References | Contains |
|---|---|---|
| Business layer | `Datapoint.Mediator.Abstractions`, and feature contracts such as `Datapoint.Mediator.Authorization` | requests, handlers, validators, authorizers |
| Host: API, CLI, MCP server, worker | `Datapoint.Mediator.DependencyInjection`, the feature and host packages | registration, authentication, configuration |

The business layer never depends on a web framework or a DI container, so every host can use it.
