# 34. Error hooks word every known error

- **Status:** Accepted
- **Date:** 2026-11-03
- **Supersedes:** [0029](0029-argument-errors-name-the-field.md)

## Context

[ADR 0029](0029-argument-errors-name-the-field.md) let applications word invalid arguments and their own exceptions, but not authentication and access-denied failures. Over HTTP that was enough, because ASP.NET Core writes those responses through `IProblemDetailsService`, where `CustomizeProblemDetails` can add the application's error code. The CLI and MCP adapters had nothing equivalent: agents got "Authentication is required." or "Access is denied." as plain text, while every other error could carry the application's code, so agents couldn't rely on one error shape.

## Decision

- Adapters map only known exceptions, each with a fixed, safe message:
  - `InvalidActionArgumentsException` → HTTP 400, CLI exit code 2, MCP error;
  - `AuthenticationRequiredException` → HTTP 401, exit code 3, MCP error;
  - `AccessDeniedException` → HTTP 403, exit code 4, MCP error.
- `InvalidActionArgumentsException` carries `ActionName`, `Field` and `Reason` ([ADR 0033](0033-argument-errors-give-a-reason.md)). `ActionArguments` builds `Field` from the `JsonException`'s path and the action's `JsonTypeInfo`: it writes only the contract's names, in the contract's spelling, stops before dictionary keys, polymorphic types and custom converters, and is `null` for unknown properties, malformed JSON and missing arguments.
- Adapters build their default text from `Field`, `Reason` and their own action name, and never print the exception's `Message`, which is for logs. A `Field` that doesn't look like a path of property names is left out.
- Applications map their own exceptions through a hook: `MapException` in ASP.NET Core, and `ErrorMessage` in the CLI and MCP adapters. A `null` result keeps Mediator's response.
- **The CLI and MCP hooks receive all three known exceptions before the adapter writes its own text.** The hook words the failure, never its outcome: the CLI keeps exit codes 2, 3 and 4, and MCP keeps the result an error. In ASP.NET Core, `MapException` still receives only invalid arguments, because a response it returns could change the status; 401 and 403 stay extensible through `CustomizeProblemDetails`.
- Anything else is not described: ASP.NET Core rethrows it to the app's exception handling, the CLI prints "An unexpected error occurred." with exit code 70, and MCP returns a generic error.
- Telemetry never records exception messages, only the exception type.

## Consequences

- Every CLI and MCP error can carry the application's code.
- A hook that handles every exception now also rewords authentication and access-denied failures. That changed during the `4.0.0` pre-releases.
- A hook can now reveal why access was denied. The authorization exceptions carry no details, but the documentation warns against adding them, such as a missing role, from elsewhere.
- Texts that Mediator writes never contain argument values, .NET type names, stack traces or paths, whoever threw the exception.
- Each adapter has regression tests that fail if exception details leak, or if a hook changes an exit code.
- A new host adapter must follow the same rules.
