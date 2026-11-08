# 40. Stable dependency releases are adopted immediately

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

Until now, a new dependency version had to be at least two weeks old before it was adopted. The wait guards against supply-chain risk: a broken or compromised release is usually found and pulled within days.

The wait also delays security fixes. `ModelContextProtocol` 2.3.1, a fix for authenticated stateful sessions, would have waited two weeks under the old rule, and so would every patch to ASP.NET Core or Entity Framework Core. The packages are used in production, so a known vulnerability left unpatched is a more concrete risk than a release that might be bad.

Keeping the wait for feature releases and skipping it for security fixes was rejected, because release notes don't always say which releases fix a vulnerability.

## Decision

- A stable release of a dependency, NuGet package or GitHub Action, can be adopted as soon as it's published.
- Pre-releases of dependencies, such as `preview`, `beta` or `rc` versions, are never adopted.
- Dependabot proposes updates weekly, with no cooldown. Its commits follow Conventional Commits: `build(deps):` for NuGet packages and `ci(deps):` for GitHub Actions.

## Consequences

- Security fixes reach `main` within a week, or sooner when merged by hand.
- A bad or compromised release can reach `main` before it's pulled. CI, the review of each Dependabot pull request, and NuGet lock files with content hashes are the remaining safeguards.
- Dependabot doesn't propose pre-releases while the current version is stable, so the pre-release rule needs no configuration. A pre-release chosen by hand is rejected in review.
