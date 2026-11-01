# Datapoint.Mediator.Abstractions

The contracts for [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator): commands, queries, stream queries and messages, their handlers, behaviours, `IMediator`, and named actions. Reference it from your business layer: it has no dependencies, so that layer stays independent of any host.

```csharp
[Action("invoices.create", Description = "Creates an invoice for a customer.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

public sealed class CreateInvoiceHandler : ICommandHandler<CreateInvoice, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateInvoice command, CancellationToken ct = default) =>
        new(Guid.CreateVersion7());
}
```

**Documentation:** [Concepts](https://github.com/datapoint-software/mediator/blob/main/docs/concepts.md) · [Actions](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md) · [Manual](https://github.com/datapoint-software/mediator/blob/main/docs/README.md)
