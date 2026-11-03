# 12. Adapters never echo input or exception details

- **Status:** Superseded by [0029](0029-argument-errors-name-the-field.md)
- **Date:** 2026-10-31

## Context

Host adapters turn failures into responses for callers who may be untrusted: HTTP clients, CLI users, and AI agents whose context may be shared or logged. Exception messages can contain connection strings, SQL, file paths or personal data. Echoing input back can enable injection, such as an attacker's text being presented to an AI agent as if it came from the server, and leaks data into logs.

## Decision

- Adapters map only known exceptions, each with a fixed, safe message:
  - `InvalidActionArgumentsException` → HTTP 400, CLI exit code 2, MCP error;
  - `AuthenticationRequiredException` → HTTP 401, exit code 3;
  - `AccessDeniedException` → HTTP 403, exit code 4.
- Applications map their own exceptions through a hook: `MapException` in ASP.NET Core, and `ErrorMessage` in the CLI and MCP adapters. The documentation shows how to report validation failures without their attempted values.
- Anything else is not described: ASP.NET Core rethrows it to the app's exception handling, the CLI prints "An unexpected error occurred." with exit code 70, and MCP returns a generic error.
- Telemetry never records exception messages, only the exception type.

## Consequences

- Unexpected failures give callers nothing to exploit, and developers find the details in their own logs and traces.
- Each adapter has a regression test that fails if exception details leak.
- A new host adapter must follow the same rules.
