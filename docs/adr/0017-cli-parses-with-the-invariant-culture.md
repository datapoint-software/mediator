# 17. The CLI parses numbers and dates with the invariant culture

- **Status:** Superseded by [0038](0038-cli-parse-errors-name-the-expected-format.md)
- **Date:** 2026-11-01

## Context

System.CommandLine converts option values with the current culture. Running the samples on a machine set to Portuguese showed that `--amount 42.5` was refused there, and `42,5` was expected. Worse, on a machine set to English, `--amount 42,5` was silently read as 425, because the comma is a thousands separator there. CLIs are used from scripts, which must behave the same on every machine. The CLI's JSON output and its `--json` input are already culture-independent.

A setting to choose the culture was considered and rejected: it would be one more public setting to keep, for a format that scripts shouldn't depend on.

## Decision

Every option whose type is a value type implementing `IParsable<T>`, other than `bool`, is parsed with the invariant culture:
- Numbers use a dot as the decimal separator, and accept no thousands separators: `NumberStyles.Float` for decimal and floating-point types, and `NumberStyles.Integer` for whole numbers.
- Dates and times are parsed with the invariant culture; the documentation asks for ISO 8601, such as `2026-11-01`.
- `bool` flags, enums and strings keep System.CommandLine's parsing, which doesn't depend on culture.

There's no setting to change this.

## Consequences

- A script gives the same result on every machine, and `42,5` is an error everywhere instead of 425 somewhere.
- Users in countries that write `42,5` must type `42.5`.
- A value that can't be parsed exits with code 1, `InvalidCommandLine`, with System.CommandLine's usual message.
