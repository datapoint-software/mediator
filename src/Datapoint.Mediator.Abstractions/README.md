# Datapoint.Mediator.Abstractions

The contracts for [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator): commands, queries and messages, their handlers, behaviours and `IMediator`.

Reference this package from your domain or application layer, where commands, queries, messages and handlers are defined. It has no dependencies, so that layer stays independent of any host (API, CLI or MCP server).

```csharp
public sealed record CreateUser(string Name) : ICommand<Guid>;

public sealed class CreateUserHandler : ICommandHandler<CreateUser, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateUser command, CancellationToken ct = default) =>
        new(Guid.NewGuid());
}
```

| Contract | Purpose |
|---|---|
| `ICommand`, `ICommand<TResult>` | Changes state; handled by exactly one `ICommandHandler`. |
| `IQuery<TResult>` | Reads state; handled by exactly one `IQueryHandler`. |
| `IStreamQuery<TResult>` | Reads state as a stream of results (`IAsyncEnumerable<TResult>`); handled by exactly one `IStreamQueryHandler`. |
| `IMessage` | Announces something that happened; handled by any number of `IMessageHandler`s. |
| `IBehavior` | Wraps handling with cross-cutting concerns such as logging or validation. |
| `IMediator` | Sends commands and queries, and publishes messages. |
| `[Action("name")]`, `IActionCatalog`, `IActionDispatcher` | Name actions, list them, and dispatch them by name with JSON arguments, for hosts such as a CLI or an MCP server. |
