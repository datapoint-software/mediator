# Telemetry

The mediator reports a trace span and a duration measurement for every dispatch, through .NET's built-in `ActivitySource` and `Meter`. Collect them with [OpenTelemetry](https://opentelemetry.io/docs/languages/dotnet/):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MediatorTelemetry.SourceName))
    .WithMetrics(m => m.AddMeter(MediatorTelemetry.MeterName))
    .UseOtlpExporter();
```

When nothing listens, the mediator skips telemetry entirely, at no cost.

## What is reported

| | Name | Details |
|---|---|---|
| Trace source | `Datapoint.Mediator` | `MediatorTelemetry.SourceName` |
| Span | `{operation} {action type}`, such as `send CreateInvoice` | one per dispatch, of kind `Internal` |
| Meter | `Datapoint.Mediator` | `MediatorTelemetry.MeterName` |
| Histogram | `datapoint.mediator.dispatch.duration` | in seconds, with the same buckets as ASP.NET Core's request durations |

Spans and measurements carry these tags:

| Tag | Value |
|---|---|
| `datapoint.mediator.operation` | `send`, `query`, `stream` or `publish` |
| `datapoint.mediator.action` | the request's full type name |
| `error.type` | on failure only: the exception's full type name. The span's status is `Error`. |

For a stream query, the span covers the whole enumeration, until the last result or a failure.

## What is never reported

Exception messages, requests, arguments and results. They can contain personal data, credentials or user input, and telemetry is often stored, shared and kept longer than application data. Use `error.type` to find failures, then your own logs for details.

## In context

Because spans nest, a request's trace shows the whole path:

```
POST /api/invoices/cancel                      (ASP.NET Core)
└── send CancelInvoice                         (Datapoint.Mediator)
    ├── SELECT … FROM Invoices                 (EF Core)
    └── publish InvoiceCancelled               (Datapoint.Mediator)
```

The durations histogram, grouped by `datapoint.mediator.action`, shows which operations are slow, and how often each one fails, by `error.type`.
