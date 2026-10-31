# Mediator

[![CI](https://github.com/datapoint-software/mediator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/datapoint-software/mediator/actions/workflows/ci.yml)
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
| `Datapoint.Mediator.Authorization.DependencyInjection` | `options.AddAuthorizationBehavior()` and `services.AddAuthorizersFromAssembly(...)`. |
| `Datapoint.Mediator.EntityFrameworkCore` | A behaviour that runs each command in an EF Core transaction and saves once, at the end. |
| `Datapoint.Mediator.EntityFrameworkCore.DependencyInjection` | `options.AddTransactionBehavior<TDbContext>()`. |

## Quick start

```csharp
// Domain layer (references Datapoint.Mediator.Abstractions)
public sealed record CreateUser(string Name) : ICommand<Guid>;

public sealed class CreateUserHandler : ICommandHandler<CreateUser, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateUser command, CancellationToken ct = default) =>
        new(Guid.NewGuid());
}

// Host (references Datapoint.Mediator.DependencyInjection)
services.AddMediator(options => options.AddHandlersFromAssemblyContaining<CreateUserHandler>());

// Anywhere with a scoped IMediator
var id = await mediator.SendAsync(new CreateUser("Ana"), ct);
```

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
