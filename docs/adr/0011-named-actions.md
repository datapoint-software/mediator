# 11. Actions are named by `[Action]`, and hidden actions look unknown

- **Status:** Superseded by [0032](0032-actions-declare-the-adapters-that-offer-them.md)
- **Date:** 2026-10-31

## Context

The aim is one business layer used from an API, a CLI and an MCP server. Each host needs to find commands and queries by a stable name, and to pass JSON arguments. Deriving names from type names would make renaming a class a breaking change for every caller, and would expose every handled type.

## Decision

- An action is a command, command with result, query or stream query marked with `[Action("name")]`. Only marked types can be dispatched by name. Messages can't be actions ([0008](0008-messages-are-not-authorized.md)).
- Names match `^[a-z0-9]+([.-][a-z0-9]+)*$`, up to 64 characters, such as `invoices.create`. Underscores aren't allowed, so every host can map names predictably: dots become path segments, subcommands or underscores in MCP tool names.
- Invalid and duplicate names, and an action implementing more than one action interface, fail at startup, in `AddMediator`.
- `[Action(..., Hidden = true)]` keeps an action out of the catalog and out of dispatch by name, while `IMediator` can still send it. An unknown and a hidden action throw the same `ActionNotFoundException`, with the same message, so callers can't discover internal actions.
- Invalid JSON arguments throw `InvalidActionArgumentsException`, whose message never repeats the input.

## Consequences

- Renaming a class doesn't change its action name, and only deliberately named types are reachable from hosts.
- Changing an action's name is a breaking change for callers, like changing an HTTP route.
- Every behaviour, including authorization and validation, applies to actions dispatched by name.
