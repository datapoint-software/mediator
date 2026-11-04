# 38. The CLI parses with the invariant culture, and its parse errors name the expected format

- **Status:** Accepted
- **Date:** 2026-11-04
- **Supersedes:** [0017](0017-cli-parses-with-the-invariant-culture.md)

## Context

[ADR 0017](0017-cli-parses-with-the-invariant-culture.md) made the CLI parse numbers and dates with the invariant culture, and kept System.CommandLine's usual message for a value that can't be parsed: "Cannot parse argument '42,5' for option '--amount' as expected type 'System.Decimal'."

That message repeats the caller's value and names a .NET type, although `MediatorCommandLineOptions.ErrorMessage` promises that the messages Mediator writes never do. A CLI can be driven by an AI agent or a script fed with untrusted text, so a message that repeats input can carry instructions back to the caller.

## Decision

Parsing is unchanged from ADR 0017. Every option whose type is a value type implementing `IParsable<T>`, other than `bool`, is parsed with the invariant culture:
- Numbers use a dot as the decimal separator, and accept no thousands separators: `NumberStyles.Float` for decimal and floating-point types, and `NumberStyles.Integer` for whole numbers.
- Dates and times are parsed with the invariant culture; the documentation asks for ISO 8601, such as `2026-11-01`.
- `bool` flags, enums and strings keep System.CommandLine's parsing, which doesn't depend on culture.

There's no setting to change this.

What changes is the message for a value Mediator's parser refuses. It names the option and what it expects, never the value given or a .NET type:
- "Option '--amount' expects a number, with '.' as the decimal separator and no thousands separators."
- "Option '--id' expects a whole number, with no thousands separators."
- "Option '--day' has a value that isn't valid. Write dates and times in ISO 8601, such as 2026-11-01."

## Consequences

- A script gives the same result on every machine, and `42,5` is an error everywhere instead of 425 somewhere.
- Users in countries that write `42,5` must type `42.5`.
- A value that can't be parsed still exits with code 1, `InvalidCommandLine`.
- System.CommandLine's own syntax errors, such as an unrecognised argument or an enum value that isn't one of the allowed ones, still repeat what was typed. They're written before any action runs, and Mediator can't reword them without replacing System.CommandLine's parsing. The rule that adapters never echo input applies to the messages Mediator writes and to every error after parsing.
