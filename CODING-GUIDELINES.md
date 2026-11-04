# Coding guidelines

This guide says how code in this repository is written: names, API design, async, errors, comments and tests. It applies to `src/`, `tests/`, `samples/` and `benchmarks/`. For building, committing and releasing, read [CONTRIBUTING.md](CONTRIBUTING.md). For how the code is organised, read [ARCHITECTURE.md](ARCHITECTURE.md).

When two rules disagree, the first one wins:

1. The [decision records](docs/adr/README.md) and the invariants in [ARCHITECTURE.md](ARCHITECTURE.md#invariants).
2. This guide, including [where it departs from Microsoft's defaults](#where-this-repository-departs-from-microsofts-defaults).
3. Microsoft's guidance, listed under [Sources](#sources).

Code that already exists isn't automatically right. If it breaks a rule here, fix it in its own commit, or say why it's an exception.

## Naming

Follow [Microsoft's naming guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-guidelines) and the [C# identifier conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names):

| Element | Style | Example |
|---|---|---|
| Namespaces, types, methods, properties, events, constants | `PascalCase` | `ActionCatalog`, `MaxNameLength` |
| Interfaces | `I` + `PascalCase` | `IActionCatalog` |
| Type parameters | `T` + `PascalCase`, or `T` alone | `TCommand`, `TResult` |
| Parameters and locals | `camelCase` | `serviceProvider`, `action` |
| Private instance fields | `_camelCase` | `_options` |
| Private static fields | `PascalCase` | `Cache` |
| Record positional parameters | `PascalCase` | `record CreateInvoice(string Customer)` |
| Test methods | `Subject_Scenario_Outcome` | `Filter_LimitsTheCommands` |

- Choose names that say what something is or does, not how it's built. Prefer clarity over brevity: `principalAccessor`, not `pa`.
- Name types and properties with nouns, methods with verbs, and Boolean properties as statements, such as `IsJoined` or `HasResult`.
- Don't abbreviate, except for widely known terms such as `Id`, `Json` or `Http`. Two-letter acronyms are all capitals (`IO`); longer ones are Pascal case (`Json`, `Mcp`).
- End attribute types with `Attribute`, exceptions with `Exception`, and asynchronous methods with `Async`.
- Don't put the type in the name (`invoiceList`): the declaration already says it.
- Don't give a type the same name as its namespace, or a name that's a keyword in another .NET language ([CA1724](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1724), [CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716)).

## Files and layout

These follow the [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions) and the [.NET runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md):

- One type per file, named after the type. Generic variants of the same name share one file, such as `ICommand` and `ICommand<TResult>` in `ICommand.cs`. The samples group a feature's small types in one file, as [their README](samples/README.md) explains.
- File-scoped namespaces (`namespace Datapoint.Mediator;`), with `using` directives outside them.
- Four spaces, no tabs. Allman braces, each on its own line. One statement per line.
- Use the language keywords (`string`, `int`), not the runtime types (`String`, `Int32`).
- Use `var` only when the type is obvious from the right-hand side, such as `new`, a cast or a literal. Otherwise write the type.
- Use modern syntax when it's clearer: collection expressions (`[]`), pattern matching, `is null`, raw string literals, and `using` declarations without braces.
- Order members: fields, constructors, properties, public methods, then private methods. Keep a private helper next to the method that uses it.

## Types and members

The packages are libraries that other teams build on, so their public API follows the [Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) ([digest](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md)).

- Keep the public API small. Make a type `public` only when users need it; everything else is `internal`. Every public line is a promise you can't take back after `4.0.0`.
- Make public classes `sealed` unless they're designed for inheritance ([why](#where-this-repository-departs-from-microsofts-defaults)). Make private and internal classes `sealed` too, or `static` when they have no instance members.
- Don't expose public fields. Use properties, and use methods for anything expensive, with side effects, or that returns a different value each time ([member design](https://learn.microsoft.com/dotnet/standard/design-guidelines/member)).
- Accept the least specific type you need (`IEnumerable<T>`) and return the most specific one that you're willing to support forever (`IReadOnlyList<T>`). Don't use `List<T>` or arrays in public signatures ([collections](https://learn.microsoft.com/dotnet/standard/design-guidelines/guidelines-for-collections)).
- Keep parameter names and order the same across overloads. Put the `CancellationToken` last ([parameters](https://learn.microsoft.com/dotnet/standard/design-guidelines/parameter-design)).
- Avoid `out` and `ref` parameters, `Boolean` parameters that switch behaviour (use an enum or two methods), and optional parameters other than the `CancellationToken`.
- Prefer immutable types: `record` or `init`-only properties, and `IReadOnly…` collections.
- Cache reflection per type in `static` fields, never per call ([ADR 0006](docs/adr/0006-cached-generic-dispatchers.md)).

## Nullability

[Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references) are on for every project, and warnings are errors.

- The annotation is part of the contract: `string?` means `null` is a valid value, `string` means it never is. Changing either one is a [breaking change](#breaking-changes).
- Check arguments of public members with `ArgumentNullException.ThrowIfNull(x)` (or `ThrowIfNullOrWhiteSpace`), because callers may have nullable warnings off ([CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062)).
- Use the [nullable attributes](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/nullable-analysis), such as `[NotNullWhen(true)]`, when the annotation alone can't describe a method.
- Don't use the null-forgiving operator (`!`) to silence a warning you can fix. When it's the only option, add a comment saying why the value can't be `null`.

## Asynchronous code

Follow the [Task-based Asynchronous Pattern](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap):

- Return `ValueTask` or `ValueTask<T>` from asynchronous APIs ([ADR 0003](docs/adr/0003-valuetask-for-asynchronous-apis.md)). Await a `ValueTask` exactly once, and never store it ([`ValueTask` remarks](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1), [CA2012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2012)).
- Take a `CancellationToken` named `cancellationToken` as the last, optional parameter, and pass it to every call that accepts one.
- In `src/`, end every `await` with `.ConfigureAwait(false)`, including `await using` and `await foreach`. Library code doesn't know whether the caller has a synchronization context, and continuing on it can deadlock or slow the caller ([CA2007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007)). Tests and samples are applications, so they don't need it.
- Never block on asynchronous code with `.Result`, `.Wait()` or `GetAwaiter().GetResult()`. Never write `async void`.
- Set an `AsyncLocal` value only inside an `async` method, so the caller gets its own value back ([ARCHITECTURE.md](ARCHITECTURE.md#authorization)). A synchronous helper may set it for an `async` method that calls it, because a value set in an `async` method that the caller awaits is lost when that method returns.

## Errors and exceptions

Follow the [exception design guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/exceptions):

- Report failures with exceptions, not error codes or `null` results.
- Throw the most specific existing exception that fits: `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, `InvalidOperationException` or `NotSupportedException` ([standard exception types](https://learn.microsoft.com/dotnet/standard/design-guidelines/using-standard-exception-types)). Never throw `Exception`, `SystemException` or `NullReferenceException`.
- Add a custom exception only when callers need to catch it separately. It ends in `Exception`, derives from `Exception`, and has the three standard constructors.
- Catch only what you can handle. Don't catch `Exception` without a filter or a rethrow, and rethrow with `throw;`, not `throw ex;`, so the stack trace survives.
- Write messages for the developer who'll read them: say what went wrong and how to fix it, such as "Register an `IPrincipalAccessor` before adding the authorization behaviour."
- Fail at startup rather than at the first request, when a configuration is invalid ([ADR 0027](docs/adr/0027-startup-checks.md)).
- **Security:** never put the caller's input, authorization details or another exception's message into a message a host adapter returns ([ADR 0034](docs/adr/0034-hooks-word-every-known-error.md)). See also Microsoft's [secure coding guidelines](https://learn.microsoft.com/dotnet/standard/security/secure-coding-guidelines).
- Document every exception a public member throws with `<exception cref="…">`.

## Comments and documentation

- Every public member has XML docs: `<summary>`, `<param>`, `<returns>`, `<exception>`. Write them for someone who can't read the source. Say what the member does and any rule the caller must follow, not how it's implemented.
- In the code, write comments that explain **why**: a decision, a constraint, a trap, or a link to an ADR. Don't repeat what the code already says.

  ```csharp
  // Bad: says what the code says.
  // Set the result.
  AuthorizationContext.Result = result;

  // Good: says why it's done this way.
  // Set inside this async method, so the value flows into next and the caller gets its own value back.
  AuthorizationContext.Result = result;
  ```

- When a comment explains what a block does, try a well-named method instead.
- Write comments as sentences: one space after `//`, a capital letter, and a full stop. Put them on their own line, not at the end of a line of code.
- Don't commit commented-out code, or `TODO` comments without an issue. Version control remembers old code.
- Exclude code from coverage only with `[ExcludeFromCodeCoverage(Justification = "...")]` and a comment ([ADR 0016](docs/adr/0016-full-line-coverage.md)).

## Readable, maintainable code

Code is read far more often than it's written, so optimise for the next reader, who may be you in a year. These rules come from the books under [Further reading](#further-reading).

- **One job per member.** A method should do one thing, at one level of detail. If you need "and" to describe it, or its body needs headings, split it. Don't split so far that the reader has to jump between many tiny methods to follow one idea: a member is too small when its name is as long as its body.
- **Hide complexity behind simple interfaces.** A good class does a lot behind a small API. Pass-through methods and classes that only rename something add reading cost without hiding anything.
- **Make the normal path obvious.** Return early for invalid input and edge cases, so the main logic isn't nested. Keep nesting to two or three levels.
- **Name things, don't explain them.** Replace a magic number with a named constant, and a complex condition with a well-named local or method.
- **Don't be clever.** Prefer plain code to tricks, terse expressions, or chains of `?:` and `??`. If the clever version is needed for performance, measure it, and say so in a comment. Debugging is harder than writing, so code written as cleverly as you can is code you can't debug.
- **Don't build for tomorrow.** Add a parameter, option or extension point when a real use needs it, not before. Each one is public API to support forever.
- **Don't repeat knowledge.** When the same rule lives in two places, they'll drift. Extract it once it appears the third time, or as soon as the copies must always change together, such as error mapping across adapters.
- **Keep side effects visible.** A method named `Get…` or `Is…` doesn't change state. Prefer passing values in and returning results over changing shared state.
- **Leave it better.** When you touch code that breaks these rules, improve it in a separate commit, so the review of your change stays readable.

## Tests

Follow Microsoft's [unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices), and the rules in [CONTRIBUTING.md](CONTRIBUTING.md#build-and-test):

- Name tests `Subject_Scenario_Outcome`, so a failure reads as a sentence: `ActionNotMarkedForCommandLine_IsNotAddedAndFailsLikeAnUnknownCommand`.
- Arrange, act, assert, in that order, separated by blank lines. One behaviour per test.
- Test behaviour through the public API, not private methods or implementation details, so refactoring doesn't break tests.
- Keep tests independent and deterministic: no shared mutable state, no real clocks or networks, no order dependence.
- Use clear, minimal data. A value that matters to the test is visible in the test; one that doesn't is built by a helper.
- Prefer real objects and small hand-written fakes to mocking frameworks.
- For security-relevant behaviour, break the code on purpose and check that a test fails (see [CLAUDE.md](CLAUDE.md#tests-and-coverage)).

## Breaking changes

During the `4.0.0` pre-releases, breaking changes are allowed but must be visible in review. After `4.0.0`, they need a new major version ([ADR 0036](docs/adr/0036-release-candidates-freeze-the-api.md)). Read Microsoft's [breaking changes guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/breaking-changes) and the [.NET runtime's breaking change rules](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md). Changes that are easy to miss:

- Renaming a public parameter, even its letter case, breaks callers that use named arguments.
- Adding a member to a public interface without a default implementation breaks every implementer.
- Sealing a class with a public constructor breaks subclasses. Unsealing doesn't break anything.
- Throwing a new exception type, unless it derives from one already thrown.
- Changing a nullability annotation, or a default value of an optional parameter.
- Changing behaviour that callers rely on, even when the old behaviour was a bug.

The build catches API signature changes through `PublicAPI.*.txt`. It can't catch behaviour changes: describe those in the commit with `!` or a `BREAKING CHANGE` footer.

## Where this repository departs from Microsoft's defaults

These are deliberate. Keep them, unless a new decision record changes them.

| Here | Microsoft's default | Why |
|---|---|---|
| Public classes are `sealed` unless designed for inheritance | "Do not seal classes without a good reason" ([sealing](https://learn.microsoft.com/dotnet/standard/design-guidelines/sealing)) | Unsealing later is compatible, sealing later is a breaking change. So the safe default for a library that must stay stable is sealed. Sealed classes are also slightly faster to call. |
| `ValueTask` for asynchronous APIs | `Task`, unless measurement shows `ValueTask` helps | Dispatch must not allocate ([ADR 0003](docs/adr/0003-valuetask-for-asynchronous-apis.md), [ADR 0006](docs/adr/0006-cached-generic-dispatchers.md)). |
| Private static fields in `PascalCase` | `s_camelCase` in the .NET runtime and C# docs | The Framework Design Guidelines don't regulate private names, and the existing code is consistent. |
| `IBehavior`'s `next` parameter | Avoid names that are keywords in other languages ([CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716)) | ASP.NET Core middleware and other mediator libraries call this delegate `next`, and the rule only affects Visual Basic implementers ([ADR 0039](docs/adr/0039-public-names-follow-microsofts-rules.md)). |
| Empty marker interfaces (`ICommand`, `IMessage`) | Avoid empty interfaces ([CA1040](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1040)) | They constrain generic methods at compile time, which is the case CA1040 allows. |
| Error hooks and the MCP serializer catch every exception | Catch specific exceptions ([CA1031](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1031)) | An exception from the app's hook or from serializing a result could carry anything, so the adapters fall back to their own safe message instead of letting it reach the caller ([ADR 0034](docs/adr/0034-hooks-word-every-known-error.md)). |
| Test names with underscores | No underscores in identifiers ([CA1707](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1707)) | Test names are read as sentences, and Microsoft's testing guidance uses this pattern. |

## Analyzers

The build already enforces: nullable warnings, XML docs on public members, the public API files (`PublicAPI.*.txt`), and the [default .NET analyzers](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview), all as errors.

The other rules in this guide are checked in review. To see what a stricter set of [quality rules (CAxxxx)](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/) would report, without changing any file:

```bash
dotnet build Datapoint.Mediator.slnx -c Release --no-incremental -p:AnalysisLevel=latest-all -p:TreatWarningsAsErrors=false
```

Enforcing them, and the [code style rules (IDExxxx)](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/) with an `.editorconfig` and [naming rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/naming-rules), is planned as a separate change. See [analyzer configuration](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options).

## Sources

Microsoft:
- [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [identifier names](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/): [naming](https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-guidelines), [types](https://learn.microsoft.com/dotnet/standard/design-guidelines/type), [members](https://learn.microsoft.com/dotnet/standard/design-guidelines/member), [exceptions](https://learn.microsoft.com/dotnet/standard/design-guidelines/exceptions), [usage](https://learn.microsoft.com/dotnet/standard/design-guidelines/usage-guidelines)
- [.NET library guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/): [breaking changes](https://learn.microsoft.com/dotnet/standard/library-guidance/breaking-changes), [versioning](https://learn.microsoft.com/dotnet/standard/library-guidance/versioning)
- [Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references) and [nullable attributes](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/nullable-analysis)
- [Task-based Asynchronous Pattern](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)
- [Code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview): [quality rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/), [style rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/)
- [Unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices) and [secure coding guidelines](https://learn.microsoft.com/dotnet/standard/security/secure-coding-guidelines)
- The .NET runtime's [coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md), [breaking change rules](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md) and [Framework Design Guidelines digest](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md)

## Further reading

- John Ousterhout, *A Philosophy of Software Design*, 2nd edition (2021): deep modules, complexity, and comments that say what the code can't.
- Dustin Boswell and Trevor Foucher, *The Art of Readable Code* (2011): code should take the least time for someone else to understand.
- Martin Fowler, *Refactoring*, 2nd edition (2018): code smells, such as long functions and mysterious names, and how to fix them safely.
- Steve McConnell, *Code Complete*, 2nd edition (2004): naming, routines, defensive programming and layout, backed by research.
- Brian Kernighan and P. J. Plauger, *The Elements of Programming Style*, 2nd edition (1978): the source of "debugging is twice as hard as writing the code".
- Vladimir Khorikov, *Unit Testing Principles, Practices, and Patterns* (2020): testing behaviour, not implementation.
- Google, [What to look for in a code review](https://google.github.io/eng-practices/review/reviewer/looking-for.html): complexity, over-engineering, naming and comments.
- Robert C. Martin, *Clean Code* (2008): widely cited, but read it critically. Its advice on very small functions is disputed by Ousterhout, and this guide follows Ousterhout.
