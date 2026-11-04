# 37. MCP errors are tool results, and nothing is rethrown to the SDK

- **Status:** Accepted
- **Date:** 2026-11-04
- **Supersedes:** [0035](0035-mcp-errors-are-tool-results.md)

## Context

[ADR 0035](0035-mcp-errors-are-tool-results.md) made the MCP adapter return the errors it describes as tool results, and rethrow every other exception, expecting the MCP SDK to report a generic error without the exception's message.

That expectation holds for most exceptions, but not for `McpException` and its subclasses: the SDK sends their message to the agent, prefixed with "An error occurred invoking '<tool>':". A handler that calls another MCP server, or an application hook that throws one, would leak its message, breaking the rule that adapters never describe an exception they don't recognise ([ADR 0034](0034-hooks-word-every-known-error.md)).

## Decision

- The adapter returns every error as a `CallToolResult` with `isError: true`, instead of throwing, so the text is exactly the message: host-provided properties, invalid arguments, authorization failures, and whatever the application's hook describes.
- An exception no hook describes is reported as "An unexpected error occurred.", by the adapter itself. Nothing is rethrown to the SDK.
- `MediatorMcpOptions.ErrorContent` returns an object instead of a string. It receives the same exceptions as `ErrorMessage` ([ADR 0034](0034-hooks-word-every-known-error.md)). Its value is serialized once with `SerializerOptions`, and sent as `structuredContent` and, as JSON, as the text, so the two can never disagree.
- The value must serialize to a JSON object. Anything else, or a value that fails to serialize, is reported as the generic error, because a hook returning the wrong shape is a bug, not something to show the agent.
- A hook that throws is treated as if it had returned `null`: Mediator's own message, or the generic error.
- Setting both hooks fails at startup ([ADR 0027](0027-startup-checks.md)), so it's never unclear which one wins.
- Mediator's own errors stay text only, so Mediator commits to no JSON error schema that it would have to keep stable.

## Consequences

- Agents can read an application's error code and field errors directly.
- An `McpException` from a handler or a hook no longer reaches the agent with its message.
- MCP clients that matched the SDK's generic error text see "An unexpected error occurred." instead. That changed during the `4.0.0` pre-releases.
- `ErrorContent` follows the same rules as `ErrorMessage`: never input, attempted values, secrets or exception messages.
- `ErrorContent` serializes with reflection when `SerializerOptions` allows it. Applications using source-generated options must include the types they return.
