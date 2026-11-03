# 30. Host registration methods carry the Mediator prefix

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

Each host adapter registers the actions with one extension method on a type it doesn't own: `MapActions` on ASP.NET Core's `IEndpointRouteBuilder`, `AddActions` on System.CommandLine's `Command`, and `WithMediatorActions` on the MCP SDK's `IMcpServerBuilder`. Only the MCP method carried the library's name.

An extension method with a generic name on a type someone else owns can clash with a method that type's library, or another library, adds later. The caller then gets an ambiguous call and has to change their code. System.CommandLine's `Command` already has an `Action` property, and the MCP SDK is still growing its `With*` methods. The names freeze when 4.0.0 is released, so this was the last chance to change them without a new major version.

## Decision

- All three methods carry the library's name, as Microsoft's own libraries do (`AddMcpServer`, `AddDbContext`):
  - `MapActions` → `MapMediatorActions`, in `MediatorEndpointRouteBuilderExtensions`;
  - `AddActions` → `AddMediatorActions`, in `MediatorCommandExtensions`;
  - `WithMediatorActions` is unchanged, in `MediatorMcpServerBuilderExtensions`.
- The extension classes keep their namespaces.
- The old names are removed, without `[Obsolete]` forwarders: this is a pre-release, and forwarders would become permanent API in 4.0.0.

## Consequences

- The three hosts are registered consistently, and a future method of ASP.NET Core, System.CommandLine or the MCP SDK can't clash with them.
- Applications on `4.0.0-beta.3` or earlier rename two calls when they upgrade.
- A new host adapter names its registration method the same way.
