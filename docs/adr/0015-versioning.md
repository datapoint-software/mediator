# 15. 4.0.0 is the first public version, versioned with MinVer

- **Status:** Superseded by [0036](0036-release-candidates-freeze-the-api.md)
- **Date:** 2026-10-31

## Context

The `Datapoint.Mediator` package ID was used on nuget.org for private, closed-source releases up to 3.0.0. Those versions can never be reused, and 3.0.0 is unlisted. The new library is a complete rewrite, released publicly from this repository.

## Decision

- The first public version is `4.0.0`, preceded by pre-releases: `4.0.0-alpha.N`, then `4.0.0-beta.N`. `4.0.0` is released once the packages have been used in a real project.
- There is no public history before `4.0.0-alpha.1`: the changelog starts there, with no migration guide from 3.x.
- Versions come from git tags through [MinVer](https://github.com/adamralph/minver). Tags have no prefix, such as `4.0.0-beta.1`. The version follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), independently of .NET's version.
- All packages are released together, with the same version, from `main`.
- Pre-releases may break the API. From `4.0.0` on, package validation compares each package with `4.0.0`, and any breaking change needs a new major version.

## Consequences

- A release is a tag on `main`, and nobody edits version numbers by hand.
- A package that didn't change still gets a new version with each release.
- Anyone who used the private 3.x packages gets no migration guide.
