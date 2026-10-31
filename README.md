# Mediator

A NuGet package that implements the Mediator pattern for .NET. It lets components send commands and queries and publish messages through a single mediator, instead of referencing each other's handlers directly, so the same business layer can be used from an API, a CLI or an MCP server.

## Packages

| Package | Purpose |
|---|---|
| `Datapoint.Mediator.Abstractions` | Contracts: commands, queries, messages, handlers, behaviours and `IMediator`. Reference it from your domain layer. |
| `Datapoint.Mediator` | The mediator implementation. |
| `Datapoint.Mediator.DependencyInjection` | `services.AddMediator(...)` for `Microsoft.Extensions.DependencyInjection`. |
| `Datapoint.Mediator.FluentValidation` | A behaviour that validates commands and queries with FluentValidation. |
| `Datapoint.Mediator.FluentValidation.DependencyInjection` | `options.AddValidationBehavior()`. |

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

## Requirements

.NET 10.

## Licence

This project is licensed under the MIT Licence.
