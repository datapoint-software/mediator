# Claude

## Mediator

This project is a NuGet package that implements the Mediator pattern. It lets components send requests and publish notifications through a single mediator, instead of referencing each other's handlers directly.

The package will be used in Datapoint's existing and future projects, so it needs to be easy to adopt and stable over time.

## Working with the contributor

Assume the contributor is a junior developer. Your job is to help them reach sound technical decisions and understand why they are sound, not only to write the code.

### Decisions

- Don't fill gaps with assumptions. When something is undecided, ask before writing code or documentation.
- For every decision, recommend one option and give the reason in a sentence. Explain trade-offs in plain language, and name the concept (for example "captive dependency" or "thread safety") so the contributor can read up on it.
- Prefer established standards, such as Microsoft's .NET conventions or Conventional Commits, over custom ones. Say which standard you are following.
- Raise risks the contributor may not see, such as performance, thread safety or breaking changes, even when they didn't ask.
- Highlight security issues as soon as you find them, even when they are outside the current task. Explain the risk, how it could be exploited, and how to fix it.
- If an answer conflicts with an earlier decision or a best practice, say so and explain the consequence before acting on it.

### Tools

- Use `EnterPlanMode` and `ExitPlanMode` for any change that touches several files or involves design decisions. Present the plan and wait for approval before changing anything.
- Use `AskUserQuestion` for decisions, not questions written in chat. Ask only what blocks the work: one round of up to four questions.
- In `AskUserQuestion`, put the recommended option first and add "(Recommended)" to its label, then the realistic alternatives. The tool adds "Other" automatically. Each option's description states its consequence in one sentence.

### Reporting

- After each change, say what changed, how you verified it, and what you couldn't verify.
- Keep explanations short and concrete. Use a small code example when it's clearer than prose.

## Branches, commits and pull requests

- Write branch names, commit messages and pull requests in English.
- `main` is the only long-lived branch.
- Name branches following [Conventional Branch 1.1.0](https://conventionalbranch.org/): `<type>/<description>`, using lowercase letters, digits and hyphens only.
- Write commit messages following [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `<type>[optional scope][!]: <description>`, with an optional body and footers.
- Commit often, one commit per self-contained change.
- Do not include attribution trailers, such as `Co-Authored-By`, in commit messages.
- Do not include attribution lines, such as "Generated with Claude Code", in pull request descriptions.

## Versioning

- Version the package following [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), independently of .NET's version numbers.
- Derive the version bump from the Conventional Commits since the last release: `fix` is a patch, `feat` is a minor, and any breaking change (`!` or a `BREAKING CHANGE` footer) is a major.
- Release from `main`. Do not create release branches per version or per target framework.
- Adding a target framework is a minor bump. Dropping one is a breaking change, so it is a major bump.
- The package currently targets `net10.0` only.

### Public API

- Each `src` project lists its public API in `PublicAPI.Shipped.txt` (released) and `PublicAPI.Unshipped.txt` (since the last release). The build fails when the public API and these files disagree.
- When you change a public API, update `PublicAPI.Unshipped.txt` in the same commit, so the change is visible in review. A removed or changed line from `PublicAPI.Shipped.txt` is a breaking change.
- On release, move the entries from `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`.

### Releasing

- Versions come from git tags via [MinVer](https://github.com/adamralph/minver). Tags have no prefix: `4.0.0-alpha.1`, not `v4.0.0-alpha.1`. Untagged builds get a pre-release version such as `4.0.0-alpha.0.12`.
- The current series is `4.0.0-alpha.N`. Release `4.0.0` once the package has been used in a real project. Versions `3.0.0` and below already exist on nuget.org and can never be reused.
- To release: move the public API entries to `PublicAPI.Shipped.txt`, commit, then tag that commit on `main` and push the tag. The `Release` workflow builds, tests and publishes to nuget.org with Trusted Publishing, after approval in the `nuget` environment.
- Never tag or push without the contributor asking. A published version is permanent.

## Tests and coverage

- New or changed behaviour comes with tests in the same change. Each test project tests one `src` project.
- CI fails when line coverage, across all packages combined, is below 95%, and reports coverage to Codecov.
- Don't write tests only to raise the percentage, such as for unused exception constructors or compiler-generated branches. Cover the situations that could hide a real bug.
- To measure coverage locally, as CI does:

  ```bash
  dotnet tool restore
  dotnet build Datapoint.Mediator.slnx -c Release
  dotnet test Datapoint.Mediator.slnx -c Release --no-build --results-directory coverage/raw --coverlet --coverlet-output-format cobertura --coverlet-include "[Datapoint.Mediator*]*" --coverlet-exclude "[*.Tests.Fixtures]*"
  dotnet reportgenerator -reports:"coverage/raw/*.cobertura.*.xml" -targetdir:coverage -reporttypes:"Html;TextSummary"
  ```
