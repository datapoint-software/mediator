# 32. Actions declare the adapters that offer them

- **Status:** Accepted
- **Date:** 2026-11-03
- **Supersedes:** [0011](0011-named-actions.md)

## Context

Every host adapter offered every action in the catalog, unless the application narrowed it with the adapter's `Filter`. So a new action was offered everywhere by default: an action meant only for the API also became a CLI command and an MCP tool, unless someone remembered to filter it out. Apiary asked for each action to say which adapters offer it. `Hidden = true` ([0011](0011-named-actions.md)) was the only way to offer an action nowhere, and it is the same decision taken for every adapter at once.

Naming the channels with strings, such as `[Channels("api", "mcp")]`, was considered. It was rejected because a typo would silently hide an action, and two custom adapters could choose the same name. Putting the channels in a property of `[Action]` was rejected as harder to read and to search for.

## Decision

- Each action is marked with one attribute per adapter that offers it: `[Api]`, `[CommandLine]` and `[ModelContextProtocol]`, in Abstractions, so the business layer doesn't reference the adapters.
- They derive from the abstract `ActionChannelAttribute`. A custom adapter defines its own sealed attribute. Channels are identified by type, so a typo is a compile error and two adapters can't collide.
- `ActionDescriptor.IsOfferedBy<TChannel>()` says whether an action is marked with that exact attribute. The descriptor reads the attributes once, when it's built.
- An adapter offers only the actions marked with its attribute, and an action it doesn't offer fails there like an unknown one. `Filter` still applies, and can only narrow that set.
- An action without any channel attribute fails at startup ([0027](0027-startup-checks.md)), with every such action listed.
- `Hidden` is removed. Code that only the application runs isn't an action: it has no `[Action]`, and is sent through `IMediator`.
- `IActionDispatcher` doesn't check channels. It dispatches any action in the catalog, and each adapter is the boundary that decides what it offers.

The rest of [0011](0011-named-actions.md) is unchanged:
- An action is a command, command with result, query or stream query marked with `[Action]`. Only marked types can be dispatched by name. Messages can't be actions ([0008](0008-messages-are-not-authorized.md)).
- Names match `^[a-z0-9]+([.-][a-z0-9]+)*$`, up to 64 characters, such as `invoices.create`. Invalid and duplicate names, and an action implementing more than one action interface, fail at startup.
- An unknown action throws `ActionNotFoundException`, whose message doesn't name it.
- Invalid JSON arguments throw `InvalidActionArgumentsException`, whose message never repeats the input ([0029](0029-argument-errors-name-the-field.md)).

## Consequences

- Forgetting a marker hides an action from that adapter rather than exposing it, and each action's declaration shows everywhere it's offered.
- Applications on `4.0.0-beta.4` or earlier mark every action, and remove `[Action]` from the ones that were hidden.
- A custom adapter must check `IsOfferedBy` with its own attribute before offering or dispatching an action. Otherwise, it offers every action.
- `IsOfferedBy` is public API, so it stays stable after 4.0.0.
