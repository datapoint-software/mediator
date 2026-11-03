# 33. Argument errors give a reason

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

[ADR 0029](0029-argument-errors-name-the-field.md) made `InvalidActionArgumentsException` name the argument that failed, but a missing value, a `null` value and a value of the wrong type all looked the same. Applications could only map them to one code, such as "invalid", and user interfaces and AI agents explain "required" better than "not valid": "Enter a hostname" instead of "This value isn't valid".

## Decision

- `InvalidActionArgumentsException` gets `Reason`, an `InvalidActionArgumentReason?`: `Missing`, `Null` or `Invalid` (wrong type or format, or any other failure). It's `null` whenever `Field` is, including for unknown properties, malformed JSON and missing arguments.
- `ActionArguments` sets it from the same walk that builds `Field`, so it comes from the action's contract and the kind of JSON value, never from the caller's text: `Missing` for a missing required property, `Null` when the value at `Field` is JSON `null`, and `Invalid` otherwise. When the walk stops before the value, such as at a dictionary, the reason is `Invalid`.
- A new constructor takes the reason. The existing ones leave it `null`, so applications that throw the exception keep working.
- Mediator's own texts say "required" for `Missing` and `Null`, in every adapter: "The argument 'x' of action 'y' is required." in the CLI and MCP, and "The argument is required." in the HTTP `errors` entry.

## Consequences

- Applications can answer "required" for `Missing` and `Null`, with the same guarantee as `Field`.
- Default texts for missing and `null` arguments changed during the `4.0.0` pre-releases. Callers that matched the old text must match the new one.
- The reason reveals nothing new: the tool schemas and OpenAPI documents already say which properties are required and nullable.
