# Datapoint.Mediator manual

Datapoint.Mediator lets you write your application's business logic once, as commands, queries and messages handled through a mediator, and offer it from an HTTP API, a command-line tool and an MCP server for AI agents, with the same authorization, validation and transactions everywhere.

## Reading order

If you're new to the library, read these in order:

1. [Getting started](getting-started.md): install the packages and send your first command.
2. [Concepts](concepts.md): commands, queries, stream queries, messages, handlers and the pipeline.
3. [Dependency injection](dependency-injection.md): registering handlers and behaviours.
4. [Behaviours](behaviors.md): the built-in behaviours, their order, and writing your own.

Then read the guides for the features you use.

## Guides

| Guide | Package |
|---|---|
| [Validation](validation.md) | `Datapoint.Mediator.FluentValidation` |
| [Authorization](authorization.md) | `Datapoint.Mediator.Authorization`, `Datapoint.Mediator.Authorization.AspNetCore` |
| [Transactions](transactions.md) | `Datapoint.Mediator.EntityFrameworkCore` |
| [Actions](actions.md) | `Datapoint.Mediator.Abstractions` |
| [ASP.NET Core](hosts/aspnetcore.md) | `Datapoint.Mediator.AspNetCore` |
| [Command line](hosts/command-line.md) | `Datapoint.Mediator.CommandLine` |
| [Model Context Protocol](hosts/model-context-protocol.md) | `Datapoint.Mediator.ModelContextProtocol` |
| [Telemetry](telemetry.md) | `Datapoint.Mediator` |
| [Testing](testing.md) | — |
| [Security](security.md) | — |
| [Use cases](use-cases.md) | — |

## Samples

The [samples](../samples/README.md) are complete, runnable apps: one invoicing business layer, offered as an HTTP API, a CLI and an MCP server. Most examples in this manual come from them.

## For maintainers

[ARCHITECTURE.md](../ARCHITECTURE.md) explains how the library works inside, [CONTRIBUTING.md](../CONTRIBUTING.md) how to build, test and release it, and the [decision records](adr/README.md) why it's built this way.
