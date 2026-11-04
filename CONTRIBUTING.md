# Contributing

This guide covers building, testing and releasing Datapoint.Mediator, and the conventions every change follows. For how the code is organised, read [ARCHITECTURE.md](ARCHITECTURE.md). For why it's built this way, read the [decision records](docs/adr/README.md).

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- The local tools, such as ReportGenerator: `dotnet tool restore`.
- For releases only, [git-cliff](https://git-cliff.org/): `brew install git-cliff`.

## Build and test

```bash
dotnet build Datapoint.Mediator.slnx -c Release
```

```bash
dotnet test Datapoint.Mediator.slnx -c Release --no-build
```

- The tests use xUnit v3 on Microsoft.Testing.Platform, which `global.json` selects.
- Each test project tests one `src` project. A test project with no tests makes the run exit with code 8, so add a test project in the same commit as its first tests.
- Warnings are errors (`TreatWarningsAsErrors`), including missing XML docs on public members.
- CI restores with `--locked-mode`. After changing a package reference, restore locally and commit the updated `packages.lock.json` files.

## Coverage

CI fails when line coverage, across all packages combined, is below **100%** ([ADR 0016](docs/adr/0016-full-line-coverage.md)). Branch coverage is reported but not gated. To measure it locally, as CI does:

```bash
dotnet tool restore
dotnet build Datapoint.Mediator.slnx -c Release
dotnet test Datapoint.Mediator.slnx -c Release --no-build --results-directory coverage/raw --coverlet --coverlet-output-format cobertura --coverlet-include "[Datapoint.Mediator*]*" --coverlet-exclude "[*.Tests.Fixtures]*"
dotnet reportgenerator -reports:"coverage/raw/*.cobertura.*.xml" -targetdir:coverage -reporttypes:"Html;TextSummary"
```

Open `coverage/index.html` to see uncovered lines. Prefer a test that checks a real situation. When code genuinely can't be tested, exclude it with `[ExcludeFromCodeCoverage(Justification = "...")]` and a comment explaining why. Never exclude code just to make CI pass.

## Benchmarks

Changes to dispatch must be measured, so the allocation-free path stays allocation-free ([ADR 0006](docs/adr/0006-cached-generic-dispatchers.md)):

```bash
dotnet run -c Release --project benchmarks/Datapoint.Mediator.Benchmarks -- --filter '*'
```

Compare the `Allocated` column before and after your change.

## Code conventions

[CODING-GUIDELINES.md](CODING-GUIDELINES.md) covers naming, API design, async, errors, comments and tests, with the Microsoft sources behind each rule. In short:

- Follow Microsoft's [.NET design guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) and naming conventions.
- Name `CancellationToken` parameters `cancellationToken`, as the last parameter, optional ([ADR 0039](docs/adr/0039-public-names-follow-microsofts-rules.md)).
- Put generic variants of the same name in one file, such as `ICommand` and `ICommand<TResult>` in `ICommand.cs`.
- Make public classes `sealed`, unless they're designed for inheritance.
- Document every public member with XML docs, including the exceptions it throws.
- Return `ValueTask` from asynchronous APIs ([ADR 0003](docs/adr/0003-valuetask-for-asynchronous-apis.md)).
- Put registration extension methods in `Microsoft.Extensions.DependencyInjection`, and endpoint ones in `Microsoft.AspNetCore.Builder`, as Microsoft's packages do.
- Manage package versions centrally in `Directory.Packages.props`. A new dependency version should be at least two weeks old.
- Pin GitHub Actions to a full commit SHA, with the version in a comment.

## Branches, commits and pull requests

- Write branch names, commit messages and pull requests in English.
- `main` is the only long-lived branch.
- Name branches following [Conventional Branch 1.1.0](https://conventionalbranch.org/): `<type>/<description>`, using lowercase letters, digits and hyphens only.
- Write commit messages following [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `<type>[optional scope][!]: <description>`. The scope is the package, in kebab case, such as `authorization` or `entity-framework-core`.
- Commit often, one commit per self-contained change, with its tests and documentation.
- Commit only when the build and the tests pass.
- Don't include attribution trailers, such as `Co-Authored-By`, or attribution lines, such as "Generated with …".

## Public API

- Each `src` project lists its public API in `PublicAPI.Shipped.txt` (released) and `PublicAPI.Unshipped.txt` (since the last release). The build fails when they disagree with the code.
- When you change a public API, update `PublicAPI.Unshipped.txt` in the same commit. A line removed from or changed in `PublicAPI.Shipped.txt` is recorded as a `*REMOVED*` line in `PublicAPI.Unshipped.txt`, and is a breaking change.
- [Package validation](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview) runs on `dotnet pack`. It has no baseline during the `4.0.0` pre-releases. Once `4.0.0` is released, set `PackageValidationBaselineVersion` to `4.0.0` in `src/Directory.Build.props`, so the pack fails on any binary break that isn't released as a new major version.

## Versioning

- Packages follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), independently of .NET's version numbers. All packages are released together, with the same version ([ADR 0015](docs/adr/0015-versioning.md)).
- The bump comes from the commits since the last release: `fix` is a patch, `feat` is a minor, and any breaking change (`!` or a `BREAKING CHANGE` footer) is a major.
- Adding a target framework is a minor bump. Dropping one is a major bump. The packages target `net10.0` only.
- Versions come from git tags through [MinVer](https://github.com/adamralph/minver). Tags have no prefix: `4.0.0-beta.1`, not `v4.0.0-beta.1`. Untagged builds get a pre-release version such as `4.0.0-beta.1.3`.
- The series is `4.0.0-alpha.N`, then `4.0.0-beta.N`, then `4.0.0-rc.N`, then `4.0.0` ([ADR 0036](docs/adr/0036-release-candidates-freeze-the-api.md)). Versions `3.0.0` and below were private releases and can never be reused.
  - A beta becomes `rc.1` once a real project has run it in production for 14 days without needing a breaking change.
  - Release candidates take only fixes and documentation that don't change the public API. A breaking change or new public API goes back to a new `beta.N`, and the 14 days start again.
  - `4.0.0` is released from a release candidate that has run in production for 14 days without needing a change to the public API.

## Releasing

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and is generated from the commits by git-cliff, configured in `cliff.toml`. It lists `feat`, `fix`, `perf` and `docs` commits, and breaking changes of any type.

1. Move the entries from each `PublicAPI.Unshipped.txt` to its `PublicAPI.Shipped.txt`, dropping `*REMOVED*` lines together with the shipped lines they remove.
2. Add the version's section to the changelog, and review it. For a release candidate and for `4.0.0`, add a line saying which packages were used in production:

   ```bash
   git cliff --unreleased --tag <version> --prepend CHANGELOG.md
   ```

3. Commit both changes on `main`, and wait for CI to pass.
4. Tag that commit and push the tag:

   ```bash
   git tag <version>
   git push origin <version>
   ```

5. The `Release` workflow builds, tests and packs, then waits for approval in the `nuget` environment. After approval, it publishes to nuget.org with Trusted Publishing, then waits, for up to an hour, until nuget.org lists every package and a new project referencing all of them restores. Only then does it create the GitHub release with the same notes, so announce the version only once the release exists: before that, consumers' restores can fail with NU1102, and NuGet's HTTP cache keeps that answer for about 30 minutes.

6. After `4.0.0-rc.1` is published, set `PackageValidationBaselineVersion` to `4.0.0-rc.1` in `src/Directory.Build.props`, so CI fails any change that breaks binary compatibility. After `4.0.0` is published, set it to `4.0.0`.

A published version is permanent: it can be unlisted, but never replaced.

## Adding a package

- [ ] `src/Datapoint.Mediator.<Name>/` with a `.csproj` (description and tags), `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` (each starting with `#nullable enable`), and a `README.md` with a quick start and a link to its guide.
- [ ] `tests/Datapoint.Mediator.<Name>.Tests/`, added in the same commit as its first tests.
- [ ] Both projects added to `Datapoint.Mediator.slnx`, and the lock files restored and committed.
- [ ] A row in the package table of the root `README.md`.
- [ ] A guide in `docs/`, linked from `docs/README.md`.
- [ ] The samples updated, if the package belongs in them.
- [ ] A decision record in `docs/adr/`, if the package involves a significant decision.
