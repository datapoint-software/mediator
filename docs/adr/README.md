# Architecture Decision Records

These records explain why Datapoint.Mediator is built the way it is. Read the relevant record before proposing to change a decided behaviour.

Each record follows [Michael Nygard's format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions): context, decision and consequences. A record is never rewritten after it's accepted. To change a decision, add a new record that supersedes it, and set the old record's status to "Superseded by NNNN".

| # | Decision | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-target-dotnet-10-only.md) | Target .NET 10 only | Accepted |
| [0003](0003-valuetask-for-asynchronous-apis.md) | `ValueTask` for every asynchronous API | Accepted |
| [0004](0004-package-split.md) | Separate packages for contracts, implementation and DI registration | Accepted |
| [0005](0005-one-behavior-interface.md) | One `IBehavior` with an overload per kind | Accepted |
| [0006](0006-cached-generic-dispatchers.md) | Cached generic dispatchers, allocation-free without behaviours | Accepted |
| [0007](0007-authorization-fails-closed.md) | Authorization fails closed | Superseded by [0018](0018-authorizers-return-decisions.md) |
| [0008](0008-messages-are-not-authorized.md) | Messages are not authorized | Accepted |
| [0009](0009-messages-run-in-the-callers-transaction.md) | Messages run immediately, inside the caller's transaction | Accepted |
| [0010](0010-transaction-retries-and-nesting.md) | `TransactionBehavior` retries the whole command, and nested commands join | Superseded by [0024](0024-read-only-query-transactions.md) |
| [0011](0011-named-actions.md) | Actions are named by `[Action]`, and hidden actions look unknown | Superseded by [0032](0032-actions-declare-the-adapters-that-offer-them.md) |
| [0012](0012-adapters-never-echo-input-or-exceptions.md) | Adapters never echo input or exception details | Superseded by [0029](0029-argument-errors-name-the-field.md) |
| [0013](0013-adapters-match-authorization-exceptions-by-name.md) | Adapters recognise authorization exceptions by type name | Accepted |
| [0014](0014-one-principal-accessor.md) | One `IPrincipalAccessor` per application | Accepted |
| [0015](0015-versioning.md) | 4.0.0 is the first public version, versioned with MinVer | Accepted |
| [0016](0016-full-line-coverage.md) | 100% line coverage, enforced in CI | Accepted |
| [0017](0017-cli-parses-with-the-invariant-culture.md) | The CLI parses numbers and dates with the invariant culture | Accepted |
| [0018](0018-authorizers-return-decisions.md) | Authorizers return decisions with details | Superseded by [0025](0025-decisions-bound-to-their-request.md) |
| [0019](0019-decisions-are-available-and-observed.md) | Authorization decisions are available to the dispatch, and observed | Superseded by [0025](0025-decisions-bound-to-their-request.md) |
| [0020](0020-commands-authorized-inside-their-transaction.md) | Commands and queries can be authorized inside their transaction | Accepted |
| [0021](0021-transaction-behavior-options.md) | `TransactionBehavior` is configured with options | Accepted |
| [0022](0022-transaction-initializers.md) | Transaction initializers run after BEGIN, on every attempt and on join | Accepted |
| [0023](0023-whoever-begins-a-transaction-commits-it.md) | Whoever begins a transaction saves and commits it | Accepted |
| [0024](0024-read-only-query-transactions.md) | Queries can run in read-only transactions, opt-in | Accepted |
| [0025](0025-decisions-bound-to-their-request.md) | Authorization decisions are bound to their request | Accepted |
| [0026](0026-transaction-context-in-abstractions.md) | The transaction state is a contract in Abstractions | Accepted |
| [0027](0027-startup-checks.md) | Startup checks, and behaviours with settings are registered once | Accepted |
| [0028](0028-host-provided-properties.md) | Host-provided properties are hidden and refused by the CLI and MCP adapters | Accepted |
| [0029](0029-argument-errors-name-the-field.md) | Argument errors name the field, and hooks see them first | Superseded by [0034](0034-hooks-word-every-known-error.md) |
| [0030](0030-registration-methods-carry-the-mediator-prefix.md) | Host registration methods carry the Mediator prefix | Accepted |
| [0031](0031-every-action-has-a-description.md) | Every action has a description | Accepted |
| [0032](0032-actions-declare-the-adapters-that-offer-them.md) | Actions declare the adapters that offer them | Accepted |
| [0033](0033-argument-errors-give-a-reason.md) | Argument errors give a reason | Accepted |
| [0034](0034-hooks-word-every-known-error.md) | Error hooks word every known error | Accepted |
| [0035](0035-mcp-errors-are-tool-results.md) | MCP errors are tool results, with optional structured content | Accepted |

To add a record, copy the structure of an existing one, use the next number, and add it to this table.
