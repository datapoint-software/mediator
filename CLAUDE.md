# Claude

## Mediator

This project is a set of NuGet packages that implement the Mediator pattern for .NET. Components send commands and queries and publish messages through a single mediator, instead of referencing each other's handlers directly. Named actions let one business layer be offered from an HTTP API, a CLI and an MCP server, with the same authorization, validation and transactions.

The packages are used in Datapoint's existing and future projects, so they need to be easy to adopt and stable over time.

Where to find things:
- [CONTRIBUTING.md](CONTRIBUTING.md): build, test, coverage, benchmarks, code conventions, commits, public API, versioning and releasing. Follow it; this file doesn't repeat it.
- [ARCHITECTURE.md](ARCHITECTURE.md): how the code is organised, and the invariants every change must keep.
- [docs/adr/](docs/adr/README.md): why things are the way they are.
- [docs/](docs/README.md): the user manual. [samples/](samples/README.md): runnable apps that use the packages.

## Working with the contributor

Assume the contributor is a junior developer. Your job is to help them reach sound technical decisions and understand why they are sound, not only to write the code.

### Decisions

- Don't fill gaps with assumptions. When something is undecided, ask before writing code or documentation.
- For every decision, recommend one option and give the reason in a sentence. Explain trade-offs in plain language, and name the concept (for example "captive dependency" or "thread safety") so the contributor can read up on it.
- Prefer established standards, such as Microsoft's .NET conventions or Conventional Commits, over custom ones. Say which standard you are following.
- Raise risks the contributor may not see, such as performance, thread safety or breaking changes, even when they didn't ask.
- Highlight security issues as soon as you find them, even when they are outside the current task. Explain the risk, how it could be exploited, and how to fix it.
- If an answer conflicts with an earlier decision or a best practice, say so and explain the consequence before acting on it.
- Before proposing to change a decided behaviour, read its record in `docs/adr/`. If it's still worth changing, say which record it reverses and why. A new or reversed decision gets a new record that supersedes the old one; accepted records are never rewritten.

### Tools

- Use `EnterPlanMode` and `ExitPlanMode` for any change that touches several files or involves design decisions. Present the plan and wait for approval before changing anything.
- Use `AskUserQuestion` for decisions, not questions written in chat. Ask only what blocks the work: one round of up to four questions.
- In `AskUserQuestion`, put the recommended option first and add "(Recommended)" to its label, then the realistic alternatives. The tool adds "Other" automatically. Each option's description states its consequence in one sentence.

### Reporting

- After each change, say what changed, how you verified it, and what you couldn't verify.
- Keep explanations short and concrete. Use a small code example when it's clearer than prose.

## Commits, pushes and releases

- Follow the branch, commit and pull request rules in [CONTRIBUTING.md](CONTRIBUTING.md#branches-commits-and-pull-requests): English, Conventional Branch 1.1.0, Conventional Commits 1.0.0, `main` as the only long-lived branch.
- Commit often, one commit per self-contained change, with its tests and documentation.
- Commit only when the build and the test command themselves exit with code 0. Never decide from filtered output, such as the result of `grep`.
- Do not include attribution trailers, such as `Co-Authored-By`, in commit messages, or attribution lines, such as "Generated with Claude Code", in pull request descriptions.
- Never push, tag or release without the contributor asking. A published version is permanent.
- After pushing, check that CI passes on the pushed commit.

## Rules for every change

### Tests and coverage

- New or changed behaviour comes with tests in the same commit. Each test project tests one `src` project.
- Line coverage must stay at 100% ([ADR 0016](docs/adr/0016-full-line-coverage.md)). Prefer a test of a real situation. Exclude code only when it genuinely can't be tested, with `[ExcludeFromCodeCoverage(Justification = "...")]` and a comment. Never exclude code to make CI pass.
- For security-relevant behaviour, such as authorization or error mapping, check that a test fails when the behaviour is broken: break it temporarily, run the tests, and restore it.

### Public API and versioning

- Update `PublicAPI.Unshipped.txt` in the same commit as any public API change. Removing or changing a shipped line is a breaking change.
- Say when a change is breaking, before making it. During the `4.0.0` pre-releases, breaking changes are allowed but must be visible in review. After `4.0.0`, they need a new major version.
- Follow the [versioning](CONTRIBUTING.md#versioning) and [releasing](CONTRIBUTING.md#releasing) steps in CONTRIBUTING.md. The next releases are `4.0.0-beta.N`; release `4.0.0` once the packages have been used in a real project.

### Documentation

Documentation is part of the change, in the same commit:
- A change to public behaviour updates its guide in `docs/`, the XML docs, and the package README if its quick start changes.
- A change to the public API updates the samples, if they use it. The samples must keep building, because CI builds them.
- A new package follows the [checklist](CONTRIBUTING.md#adding-a-package) in CONTRIBUTING.md.
- A change to how the code works inside updates ARCHITECTURE.md.
- Package READMEs link to the manual with full GitHub URLs, because nuget.org can't follow relative links.

### Security invariants

These must hold after every change. Each has tests, and a record explaining why:
- Authorization fails closed: an action without an authorizer is denied ([ADR 0025](docs/adr/0025-decisions-bound-to-their-request.md)).
- Messages aren't authorized and can never be actions, so they must never be built from user input ([ADR 0008](docs/adr/0008-messages-are-not-authorized.md)).
- Hidden and unknown actions fail the same way ([ADR 0011](docs/adr/0011-named-actions.md)).
- Host adapters never echo the caller's input or the message of an exception they don't recognise, and telemetry never records exception messages ([ADR 0012](docs/adr/0012-adapters-never-echo-input-or-exceptions.md)).
- Every host adapter maps `AuthenticationRequiredException` and `AccessDeniedException` by their full type names ([ADR 0013](docs/adr/0013-adapters-match-authorization-exceptions-by-name.md)).
- Only one `IPrincipalAccessor` can be registered ([ADR 0014](docs/adr/0014-one-principal-accessor.md)).

A new host adapter needs a regression test proving it doesn't leak exception details.

### Performance

- Dispatch without behaviours must stay allocation-free ([ADR 0006](docs/adr/0006-cached-generic-dispatchers.md)). Measure changes to dispatch with the benchmarks, before and after, and report the `Allocated` column.
- Cache reflection per type, in `static` fields, never per dispatch.

### Dependencies and CI

- Package versions live in `Directory.Packages.props`. After changing one, restore and commit the updated `packages.lock.json` files, because CI restores in locked mode.
- Choose dependency versions released at least two weeks ago.
- Pin GitHub Actions to a full commit SHA, with the version in a comment.
- Treat secrets in workflows as environment variables, never interpolated into scripts, and give each job the least permissions it needs.
