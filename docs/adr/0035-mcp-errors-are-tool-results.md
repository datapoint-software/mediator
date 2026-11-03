# 35. MCP errors are tool results, with optional structured content

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

The MCP adapter reported errors by throwing `McpException` with its message. The MCP SDK turns that into a tool result marked as an error, but prefixes the text with "An error occurred invoking '<tool>':". Applications that return JSON from `ErrorMessage`, such as an error code and field errors, made agents strip the prefix before parsing it. HTTP callers, by contrast, read `ProblemDetails` directly.

MCP tool results have a `structuredContent` member, a JSON object, and the specification recommends also sending it as text for clients that only read text.

## Decision

- The adapter returns every error it describes as a `CallToolResult` with `isError: true`, instead of throwing, so the text is exactly the message: host-provided properties, invalid arguments, authorization failures, and whatever the application's hook describes.
- A new hook, `MediatorMcpOptions.ErrorContent`, returns an object instead of a string. It receives the same exceptions as `ErrorMessage` ([ADR 0034](0034-hooks-word-every-known-error.md)). Its value is serialized once with `SerializerOptions`, and sent as `structuredContent` and, as JSON, as the text, so the two can never disagree.
- The value must serialize to a JSON object. Anything else is reported as a generic error, as for an unknown exception, because a hook returning the wrong shape is a bug, not something to show the agent.
- `ErrorMessage` keeps working as before, apart from the prefix. Setting both hooks fails at startup ([ADR 0027](0027-startup-checks.md)), so it's never unclear which one wins.
- Mediator's own errors stay text only, so Mediator commits to no JSON error schema that it would have to keep stable.
- Exceptions no hook describes are still rethrown, and the SDK reports its generic error without their message.

## Consequences

- Agents can read an application's error code and field errors directly.
- MCP clients that matched the SDK's prefix see different text. That changed during the `4.0.0` pre-releases.
- `ErrorContent` follows the same rules as `ErrorMessage`: never input, attempted values, secrets or exception messages.
- `ErrorContent` serializes with reflection when `SerializerOptions` allows it. Applications using source-generated options must include the types they return.
