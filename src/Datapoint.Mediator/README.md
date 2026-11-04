# Datapoint.Mediator

A fast Mediator pattern implementation for commands, queries, stream queries and messages, with behaviour pipelines and OpenTelemetry tracing and metrics. Most apps register it with [Datapoint.Mediator.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.DependencyInjection).

```csharp
var id = await mediator.SendAsync(new CreateInvoice("Ana", 100m), cancellationToken);
var invoice = await mediator.QueryAsync(new GetInvoice(id), cancellationToken);
await mediator.PublishAsync(new InvoiceCreated(id), cancellationToken);

await foreach (var item in mediator.StreamAsync(new ListInvoices(), cancellationToken)) { }
```

Dispatch adds no allocations when no behaviours are registered.

**Documentation:** [Concepts](https://github.com/datapoint-software/mediator/blob/main/docs/concepts.md) · [Telemetry](https://github.com/datapoint-software/mediator/blob/main/docs/telemetry.md) · [Manual](https://github.com/datapoint-software/mediator/blob/main/docs/README.md)
