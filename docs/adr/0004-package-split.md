# 4. Separate packages for contracts, implementation and DI registration

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

A domain layer should depend on as little as possible: it defines commands, queries and handlers, but shouldn't depend on the mediator's implementation, a DI container, ASP.NET Core or EF Core. Hosts, on the other hand, need the implementation and its registration.

## Decision

- `Datapoint.Mediator.Abstractions` holds the contracts. Domain layers reference only this package, plus feature contracts such as `Datapoint.Mediator.Authorization`.
- `Datapoint.Mediator` holds the implementation.
- Each feature is split the same way: the feature package, such as `Datapoint.Mediator.FluentValidation`, and a `.DependencyInjection` package with its registration methods.
- Host adapters (`AspNetCore`, `CommandLine`, `ModelContextProtocol`) and host-specific helpers (`Authorization.AspNetCore`) are separate packages.

## Consequences

- Apps only pull in what they use, and a domain layer never depends on a web framework or DI container.
- There are more packages, 13 at the time of writing, which are all released together with the same version.
- Adapters depend only on `Abstractions`, which is why they recognise authorization exceptions by name ([0013](0013-adapters-match-authorization-exceptions-by-name.md)).
