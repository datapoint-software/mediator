# 28. Host-provided properties are hidden and refused by the CLI and MCP adapters

- **Status:** Accepted
- **Date:** 2026-11-02

## Context

Some properties of an action are filled in by the host, never by the caller. For example, a multi-tenant API overwrites the `tenantId` of every request with the tenant of the caller's verified session, so the caller can never choose it. The adapters treated those properties as ordinary input: the CLI required an option such as `--tenant-id`, the MCP tool's input schema asked agents for a value, and OpenAPI documented it as an input. The value was discarded anyway, and asking for it suggested that the caller chose a security-sensitive value.

The workarounds don't fit: `[JsonIgnore]` also stops the host binding the property, and a default value on the record parameter changes the contract for every host and still shows the option.

## Decision

- `[HostProvided]`, in `Datapoint.Mediator.Abstractions`, marks a property or a positional record's parameter as supplied by the host. It has an optional `Reason`, for documentation, and knows nothing about tenants or any other concept. Only the action's own top-level properties count.
- `ActionDescriptor.HostProvidedProperties` lists the marked properties, found once when the descriptor is built, so every host applies the same rule.
- The declaration is an attribute on the contract, not an adapter option, because one contracts assembly is usually shared by the API, CLI and MCP hosts: one attribute covers all of them, and no host can forget it.
- **CLI:** no option, never required. `--json` containing the property is refused with `'<name>' is set by the server and cannot be given.` and exit code 2.
- **MCP:** left out of the tool's input schema. A tool call containing the property is refused with the same message, rather than silently dropped: neither the agent nor the user should believe they chose the value, and a refusal makes a confused agent, or an injected instruction that tries to choose it, visible.
- Both adapters match the property's JSON name, from the host's serializer options, with `StringComparer.OrdinalIgnoreCase`, the comparison System.Text.Json uses for case-insensitive binding, so the refusal and the binder never disagree. The message names the contract's JSON name, never the caller's spelling ([0012](0012-adapters-never-echo-input-or-exceptions.md)).
- **ASP.NET Core, `IActionDispatcher` and `IMediator` don't change.** The host sets or overwrites the value before dispatch: in middleware for an API, or in a decorator of `IActionDispatcher` for in-process hosts. The adapters check the caller's arguments before they call the dispatcher, so a decorator can add the value afterwards without tripping the refusal.
- OpenAPI marks the property `readOnly`, which means "not sent in requests". `Datapoint.Mediator.AspNetCore` doesn't depend on `Microsoft.AspNetCore.OpenApi`, so the guide and the sample show a schema transformer instead of the package shipping one.

## Consequences

- Hiding is presentation only. A host that doesn't set the value dispatches the property's default, such as `Guid.Empty`, so handlers and authorizers should treat a default value as invalid.
- A host that wrote host-provided values into the caller's CLI or MCP arguments must move that into an `IActionDispatcher` decorator: otherwise every call is refused.
- A C# `required` property that's left out fails to deserialize when no host sets it.
- The refusal says "server", which is what users of a remote API understand, even for a CLI that dispatches in-process.
