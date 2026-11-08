---
name: prepare-release
description: Prepare and publish the next version of the Datapoint.Mediator packages: pick the version, move the shipped API, write the changelog, commit, push, tag, and follow the Release workflow. Use when the contributor asks to prepare, cut, tag or publish a release or a new version.
---

# Releasing

The steps are in [CONTRIBUTING.md](../../../CONTRIBUTING.md#releasing). This skill adds the decisions and checks around them. A published version is permanent, so never tag or push without the contributor asking.

## 1. Choose the version

Versions come from git tags through MinVer. No file holds the version number, so a release is a commit and a tag.

1. Find the last release and what changed since:
   ```bash
   git fetch --tags origin
   git describe --tags --abbrev=0
   git log --oneline <last>..origin/main
   ```
2. Apply the series rules in [ADR 0036](../../../docs/adr/0036-release-candidates-freeze-the-api.md), and check the dates in the changelog:
   - During betas the next version is `4.0.0-beta.N+1`, unless the 14 days in production have passed since the last beta with a breaking change. Then it's `4.0.0-rc.1`.
   - A release candidate takes only fixes and docs that don't change the public API. Any breaking change or new public API since the last release candidate means the next version is a beta again.
   - After `4.0.0`, use Semantic Versioning: `fix` is a patch, `feat` a minor, and a breaking change a major.
3. If there are no commits that package users would notice, such as code, dependency changes or user docs, say so and ask whether to release at all.

Ask the contributor to confirm the version, and whether to push the tag or only the release commit.

## 2. Prepare

Start on an up-to-date `main` with a clean working tree, then run:

```bash
scripts/prepare-release.sh <version>
```

The script checks the repository and the version, moves each `PublicAPI.Unshipped.txt` into its `PublicAPI.Shipped.txt`, and adds the version's section to `CHANGELOG.md` with git-cliff. It doesn't commit, tag or push.

## 3. Review the changelog

git-cliff lists only `feat`, `fix`, `perf` and `docs` commits, and breaking changes. Read the new section as a package user would, and edit only that section:

- Remove entries that only matter to contributors, such as Claude Code skills, CI or repository tooling.
- Add a `### Changed` entry for dependency changes that users will notice, such as a new minimum version of a package the packages depend on. `build(deps)` commits aren't listed by git-cliff.
- For a release candidate and for `4.0.0`, add a line saying which packages were used in production.
- Keep the [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) headings: Added, Changed, Deprecated, Removed, Fixed, Security.

Never change earlier sections: they are the notes of published versions.

## 4. Commit and push

1. Build and test. Commit only if both commands exit with code 0:
   ```bash
   dotnet build Datapoint.Mediator.slnx -c Release
   dotnet test Datapoint.Mediator.slnx -c Release --no-build
   ```
2. Commit, with no attribution trailers: `git commit -am "chore(release): <version>"`.
3. Push `main` and wait for CI to pass on that commit:
   ```bash
   git push origin main
   gh run list --commit "$(git rev-parse HEAD)" --json databaseId
   gh run watch <id> --exit-status
   ```

## 5. Tag

Only once CI has passed, and only if the contributor asked for the tag:

```bash
git tag <version>
git push origin <version>
```

The `Release` workflow then builds, tests and packs, and waits for the contributor's approval in the `nuget` environment. Give them the run's link (`gh run list --workflow Release --limit 1`). Approving it is their decision, because publishing can't be undone. After approval, the workflow publishes, waits until nuget.org serves every package, and creates the GitHub release. Announce the version only once the GitHub release exists.

## 6. After a release candidate or 4.0.0

Once `4.0.0-rc.1` is published, set the package validation baseline so CI fails any change that breaks binary compatibility. Do the same with `4.0.0` once it's published:

```bash
scripts/set-validation-baseline.sh <version>
```

Pack to check it (`dotnet pack Datapoint.Mediator.slnx -c Release`), then commit it as `build: validate packages against <version>`.

## 7. Report

Say which version was prepared, the release commit, what the changelog says, what CI showed, whether the tag was pushed, and what's left for the contributor, such as approving the `nuget` environment.
