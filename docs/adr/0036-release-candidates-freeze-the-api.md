# 36. Release candidates freeze the API before 4.0.0

- **Status:** Accepted
- **Date:** 2026-11-04
- **Supersedes:** [0015](0015-versioning.md)

## Context

[0015](0015-versioning.md) released `4.0.0` once the packages had been used in a real project, straight after the betas. They now run in production, in an application that manages a Docker Swarm cluster, but that use is still changing the API: `4.0.0-beta.2` to `4.0.0-beta.6` had 15 breaking changes between them, 8 of them in the last two. Released now, `4.0.0` would likely need a `5.0.0` within weeks.

A release candidate is a version that becomes the final one unless a bug is found, so its API must not change. Letting a release candidate break the API was rejected, because adopters could no longer rely on one. Letting it add API was rejected, because `4.0.0` would then contain API that nobody has used. Waiting until every package is used in production was rejected, because a package such as the CLI or MCP adapter could hold back `4.0.0` indefinitely.

## Decision

- The first public version is `4.0.0`, preceded by pre-releases: `4.0.0-alpha.N`, then `4.0.0-beta.N`, then `4.0.0-rc.N`.
- A beta becomes `4.0.0-rc.1` once a real project has run it in production for 14 days without needing a breaking change.
- Release candidates take only fixes and documentation that don't change the public API, released as the next `rc.N`. A breaking change or new public API goes back to a new `beta.N`, and the 14 days start again from that beta.
- `4.0.0` is released from a release candidate that has run in production for 14 days without needing a change to the public API.
- In the first commit after `4.0.0-rc.1` is published, `PackageValidationBaselineVersion` is set to `4.0.0-rc.1`, so a pack that breaks binary compatibility fails in CI. After `4.0.0` is released, the baseline moves to `4.0.0`.
- Packages the production application doesn't use are released at `4.0.0` too, backed by their tests and the samples. The changelog of each release candidate and of `4.0.0` says which packages were used in production.
- There is no public history before `4.0.0-alpha.1`: the changelog starts there, with no migration guide from 3.x.
- Versions come from git tags through [MinVer](https://github.com/adamralph/minver). Tags have no prefix, such as `4.0.0-rc.1`. The version follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), independently of .NET's version.
- All packages are released together, with the same version, from `main`.
- From `4.0.0` on, any breaking change needs a new major version.

## Consequences

- If the API holds, `4.0.0-rc.1` comes no earlier than 2026-11-17, 14 days after `4.0.0-beta.6`, and `4.0.0` no earlier than 14 days after that.
- A breaking change found during the release candidates costs at least 14 more days.
- Adopters can build on a release candidate and expect no breaking changes before `4.0.0`.
- Packing needs the baseline package from nuget.org once the baseline is set.
- Which packages were used in production is recorded in the changelog, not here, because an accepted record is never rewritten.
- A release is a tag on `main`, and nobody edits version numbers by hand.
- A package that didn't change still gets a new version with each release.
