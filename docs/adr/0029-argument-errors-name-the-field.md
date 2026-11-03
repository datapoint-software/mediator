# 29. Argument errors name the field, and hooks see them first

- **Status:** Accepted
- **Date:** 2026-11-03
- **Supersedes:** [0012](0012-adapters-never-echo-input-or-exceptions.md)

## Context

[ADR 0012](0012-adapters-never-echo-input-or-exceptions.md) made adapters report invalid arguments with a fixed message, "The arguments for action '<name>' are not valid." That leaked nothing, but it didn't say which argument was wrong, so AI agents and API callers couldn't correct their call, and applications couldn't attach the error to a field. Adapters also printed the exception's `Message` before the application's hook ran, so applications couldn't map the failure to their own error codes, and the guarantee depended on whoever threw the exception, since its constructors are public.

System.Text.Json's `JsonException` can't be shown as it is: its message names .NET types, and its `Path` uses the caller's spelling and contains dictionary keys, which the caller chose.

## Decision

- Adapters map only known exceptions, each with a fixed, safe message:
  - `InvalidActionArgumentsException` → HTTP 400, CLI exit code 2, MCP error;
  - `AuthenticationRequiredException` → HTTP 401, exit code 3;
  - `AccessDeniedException` → HTTP 403, exit code 4.
- `InvalidActionArgumentsException` carries `ActionName` and `Field`. `ActionArguments`, which the dispatcher uses, builds `Field` from the `JsonException`'s path and the action's `JsonTypeInfo`: it writes only the contract's names, in the contract's spelling, stops before dictionary keys, polymorphic types and custom converters, and is `null` for unknown properties, malformed JSON and missing arguments.
- Adapters build their default text from `Field` and their own action name, and never print the exception's `Message`, which is for logs. A `Field` that doesn't look like a path of property names is left out, in case an application's own exception carries something else.
  - HTTP: a `ValidationProblemDetails` keyed by the field, or a `ProblemDetails` without one. There's no custom `code` member: RFC 9457 identifies a problem by its `type`, and applications add their own codes.
  - CLI and MCP: "The argument '<field>' of action '<name>' is not valid.", or the generic text.
- Applications map their own exceptions through a hook: `MapException` in ASP.NET Core, and `ErrorMessage` in the CLI and MCP adapters. The hook also receives `InvalidActionArgumentsException` before the adapter writes its own response, and a `null` result keeps it. Authentication and access-denied responses can't be replaced, but ASP.NET Core writes every response of its own through `IProblemDetailsService`, so `CustomizeProblemDetails` can extend them.
- Anything else is not described: ASP.NET Core rethrows it to the app's exception handling, the CLI prints "An unexpected error occurred." with exit code 70, and MCP returns a generic error.
- Telemetry never records exception messages, only the exception type.
- Default serializer options don't change. The documentation recommends options based on `JsonSerializerOptions.Strict`, with camelCase names.

## Consequences

- Callers learn which argument failed, never its value, and applications can report it with their own codes.
- Texts that Mediator writes never contain argument values, .NET type names, stack traces or paths, whoever threw the exception.
- Argument names are reported before authorization runs, so a caller who may not run an action can learn the names of its properties. They're already published in OpenAPI documents and MCP tool schemas, so this reveals nothing new for visible actions, and hidden actions still fail as unknown ones before their arguments are read.
- An application that throws `InvalidActionArgumentsException` itself, and wants its own text shown, returns it from its hook.
- A hook that handles every exception now also replaces the response for invalid arguments.
- Each adapter has regression tests that fail if exception details, or an application's unsafe field, leak.
- A new host adapter must follow the same rules.
