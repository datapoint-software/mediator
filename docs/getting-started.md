# Getting started

## Requirements

.NET 10. The packages target `net10.0` only.

## Install

Your business layer, where commands, queries and handlers live, needs the contracts:

```bash
dotnet add package Datapoint.Mediator.Abstractions --prerelease
```

The app that runs them, such as an API, needs the implementation and its registration:

```bash
dotnet add package Datapoint.Mediator.DependencyInjection --prerelease
```

`--prerelease` is needed until `4.0.0` is released.

## Your first command

A command changes state. Declare it as a record, and write one handler for it:

```csharp
using Datapoint.Mediator;

public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

public sealed class CreateInvoiceHandler(InvoicingDbContext db) : ICommandHandler<CreateInvoice, Guid>
{
    public async ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken cancellationToken = default)
    {
        var invoice = new Invoice { Id = Guid.CreateVersion7(), Customer = command.Customer, Amount = command.Amount };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}
```

Register the mediator and every handler in the assembly:

```csharp
builder.Services.AddMediator(o => o.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>());
```

Send the command through `IMediator`, from anywhere with a DI scope, such as an endpoint or a controller:

```csharp
app.MapPost("/invoices", async (CreateInvoice command, IMediator mediator, CancellationToken cancellationToken) =>
    await mediator.SendAsync(command, cancellationToken));
```

The endpoint doesn't know which class handles `CreateInvoice`, and the handler doesn't know it was called from HTTP.

## Your first query

A query reads state and returns a result:

```csharp
public sealed record GetInvoice(Guid Id) : IQuery<InvoiceView?>;

public sealed class GetInvoiceHandler(InvoicingDbContext db) : IQueryHandler<GetInvoice, InvoiceView?>
{
    public async ValueTask<InvoiceView?> HandleAsync(GetInvoice query, CancellationToken cancellationToken = default) =>
        await db.Invoices
            .Where(i => i.Id == query.Id)
            .Select(i => new InvoiceView(i.Id, i.Customer, i.Amount))
            .SingleOrDefaultAsync(cancellationToken);
}
```

```csharp
var invoice = await mediator.QueryAsync(new GetInvoice(id), cancellationToken);
```

`AddHandlersFromAssemblyContaining` already registered the new handler.

## Next steps

- [Concepts](concepts.md) explains the four kinds of request and how the pipeline works.
- [Behaviours](behaviors.md) adds authorization, validation and transactions around every handler.
- [Actions](actions.md) offers the same commands and queries from an API, a CLI and an MCP server, without writing an endpoint for each.
- The [samples](../samples/README.md) show all of it working together.
