# 39. Public names and parameters follow Microsoft's design guidelines

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

An audit against [CODING-GUIDELINES.md](../../CODING-GUIDELINES.md) found public API that departs from Microsoft's [Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) and the .NET code analysis rules:

- `CancellationToken` parameters were named `ct`, a convention of this repository, while every .NET library uses `cancellationToken` ([parameter naming](https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-parameters), no abbreviations).
- `IAuthorizationContext.For` is a method named with a preposition, and a Visual Basic keyword ([CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716)).
- The class `Mediator` has the same name as its namespace, `Datapoint.Mediator` ([CA1724](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1724)).
- Several public methods take optional parameters other than the token, which the guidelines replace with overloads ([member overloading](https://learn.microsoft.com/dotnet/standard/design-guidelines/member-overloading)).
- `InvalidActionArgumentsException` has a constructor that duplicates another with one argument fewer.

Renaming a parameter breaks callers that use named arguments, and the others break source and binary compatibility. [ADR 0036](0036-release-candidates-freeze-the-api.md) makes the betas the last chance to change them without a new major version.

## Decision

All of them change in the next beta:

- `CancellationToken` parameters are named `cancellationToken`, everywhere, public or not.
- `IAuthorizationContext.For(action)` becomes `GetResultFor(action)`. The behaviour decided in [ADR 0025](0025-decisions-bound-to-their-request.md) doesn't change.
- `Mediator` becomes `ServiceProviderMediator`: a mediator that resolves handlers and behaviours from an `IServiceProvider`.
- Optional parameters other than the token are replaced with overloads. A new overload on `IActionDispatcher` is a default interface method, so existing implementations, such as an app's decorator, keep compiling.
- The duplicate `InvalidActionArgumentsException` constructor is removed.

One rule is kept as a deliberate deviation: `IBehavior`'s `next` parameter, which CA1716 flags as a Visual Basic keyword. ASP.NET Core middleware and endpoint filters, and other mediator libraries, call this delegate `next`, so readers recognise it, and the rule only affects Visual Basic implementers.

## Consequences

- The public API reads like the rest of .NET, and the analyzers report no naming issues in `src`.
- Every consumer of the betas must update named `ct:` arguments, calls to `For`, references to `Mediator`, and calls that named an optional parameter. The compiler finds them all.
- `CODING-GUIDELINES.md` lists `next` as a deviation, and no longer lists `ct`.
