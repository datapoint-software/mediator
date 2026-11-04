# Mediator

[![CI](https://github.com/datapoint-software/mediator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/datapoint-software/mediator/actions/workflows/ci.yml)
[![Coverage](https://codecov.io/gh/datapoint-software/mediator/graph/badge.svg)](https://codecov.io/gh/datapoint-software/mediator)
[![NuGet](https://img.shields.io/nuget/vpre/Datapoint.Mediator)](https://www.nuget.org/packages/Datapoint.Mediator)
[![Downloads](https://img.shields.io/nuget/dt/Datapoint.Mediator)](https://www.nuget.org/packages/Datapoint.Mediator)
[![License: MIT](https://img.shields.io/github/license/datapoint-software/mediator)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

A NuGet package that implements the Mediator pattern for .NET. It lets components send commands and queries and publish messages through a single mediator, instead of referencing each other's handlers directly, so the same business layer can be used from an API, a CLI or an MCP server.

## Packages

| Package | Purpose |
|---|---|
| `Datapoint.Mediator.Abstractions` | Contracts: commands, queries, messages, handlers, behaviours and `IMediator`. Reference it from your domain layer. |
| `Datapoint.Mediator` | The mediator implementation. |
| `Datapoint.Mediator.DependencyInjection` | `services.AddMediator(...)` for `Microsoft.Extensions.DependencyInjection`. |
| `Datapoint.Mediator.FluentValidation` | A behaviour that validates commands and queries with FluentValidation. |
| `Datapoint.Mediator.FluentValidation.DependencyInjection` | `options.AddValidationBehavior()`. |
| `Datapoint.Mediator.Authorization` | A behaviour that authorizes commands and queries for the current user, denying access by default. Reference it from your domain layer. |
| `Datapoint.Mediator.Authorization.DependencyInjection` | `options.AddAuthorizationBehavior()`, `services.AddAuthorizersFromAssembly(...)` and `services.AddFixedPrincipalAccessor(...)` for CLIs. |
| `Datapoint.Mediator.Authorization.AspNetCore` | `services.AddHttpPrincipalAccessor()`: authorizes for the ASP.NET Core request's user. |
| `Datapoint.Mediator.EntityFrameworkCore` | A behaviour that runs each command in an EF Core transaction and saves once, at the end. |
| `Datapoint.Mediator.EntityFrameworkCore.DependencyInjection` | `options.AddTransactionBehavior<TDbContext>()`. |
| `Datapoint.Mediator.ModelContextProtocol` | Offers actions as MCP tools for AI agents: `services.AddMcpServer().WithMediatorActions()`. |
| `Datapoint.Mediator.AspNetCore` | Maps actions as HTTP endpoints with OpenAPI metadata: `app.MapMediatorActions("/api")`. |
| `Datapoint.Mediator.CommandLine` | Adds actions as System.CommandLine commands: `new RootCommand().AddMediatorActions(services)`. |

## Quick start

```csharp
// Domain layer (references Datapoint.Mediator.Abstractions)
public sealed record CreateUser(string Name) : ICommand<Guid>;

public sealed class CreateUserHandler : ICommandHandler<CreateUser, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateUser command, CancellationToken cancellationToken = default) =>
        new(Guid.NewGuid());
}

// Host (references Datapoint.Mediator.DependencyInjection)
services.AddMediator(options => options.AddHandlersFromAssemblyContaining<CreateUserHandler>());

// Anywhere with a scoped IMediator
var id = await mediator.SendAsync(new CreateUser("Ana"), cancellationToken);
```

## Documentation

- The [manual](docs/README.md): getting started, concepts, and a guide per feature and host.
- The [samples](samples/README.md): one business layer offered as an HTTP API, a CLI and an MCP server.
- For contributors: [CONTRIBUTING.md](CONTRIBUTING.md), [ARCHITECTURE.md](ARCHITECTURE.md) and the [decision records](docs/adr/README.md).

## Actions: dispatch by name from an API, a CLI or an MCP server

Name a command, query or stream query with `[Action]`, and any host can dispatch it from a name and JSON arguments:

```csharp
[Api]
[ModelContextProtocol]
[Action("invoice.create", "Creates an invoice for a customer.")]
public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;

// In a host, with a scoped IActionDispatcher:
var id = await dispatcher.DispatchAsync("invoice.create", arguments, cancellationToken: cancellationToken); // arguments: a JsonElement
```

- Every behaviour still runs, so actions dispatched by name are authorized and validated like any other.
- `IActionCatalog` lists the available actions, with their names, descriptions, kinds and types, for building CLI commands, MCP tools or endpoints.
- `[Api]`, `[CommandLine]` and `[ModelContextProtocol]` say which hosts offer each action. A host offers only the actions marked for it, and one it doesn't offer fails there like an unknown action, so callers can't discover it.
- Invalid names, duplicate names, missing descriptions, actions no host offers and named messages fail at startup, in `AddMediator`. Messages can't be actions, because their handlers aren't authorized.
- Invalid JSON throws `InvalidActionArgumentsException`, whose message never repeats the input.
- **Hosts:** one line offers every action in each kind of host. See each package's README.

  | Host | Package | Registration | `invoices.create` becomes |
  |---|---|---|---|
  | API | `Datapoint.Mediator.AspNetCore` | `app.MapMediatorActions("/api")` | `POST /api/invoices/create` |
  | CLI | `Datapoint.Mediator.CommandLine` | `root.AddMediatorActions(services)` | `mytool invoices create --customer Ana --amount 100` |
  | MCP server | `Datapoint.Mediator.ModelContextProtocol` | `.WithMediatorActions()` | the tool `invoices_create` |

## Behaviour order

Behaviours run in the order they're registered, with the first one outermost. The recommended order is:

```csharp
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<CreateUserHandler>();
    options.AddBehavior<ExceptionHandlingBehavior>(); // your own: turns failures into responses
    options.AddAuthorizationBehavior();               // before anything reveals information
    options.AddValidationBehavior();                  // before a transaction is opened
    options.AddTransactionBehavior<AppDbContext>();   // innermost: wraps only the handler
});
```

## Tracing and metrics

The mediator reports a span and a duration measurement for every dispatch. Collect them with OpenTelemetry using `MediatorTelemetry.SourceName` and `MediatorTelemetry.MeterName`. Exception messages are never recorded.

## Requirements

.NET 10.

## Licence

This project is licensed under the MIT Licence.
