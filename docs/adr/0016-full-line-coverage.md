# 16. 100% line coverage, enforced in CI

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The packages are meant to be adopted across Datapoint's projects and stay stable for years, and much of their value is in edge cases: authorization failing closed, errors not leaking, transactions joining or retrying. A lower threshold, which was 95% at first, lets untested code accumulate in exactly those paths.

## Decision

- CI fails when line coverage, across all packages combined, is below 100%. It's measured with coverlet and ReportGenerator, and reported to Codecov for information.
- Branch coverage is reported but not gated, because some branches depend on timing, such as telemetry when an operation fails.
- Code that genuinely can't be tested is excluded with `[ExcludeFromCodeCoverage(Justification = "...")]` and a comment, so the exclusion is visible in review. Code is never excluded just to make CI pass.
- New or changed behaviour comes with tests in the same commit.

## Consequences

- Every line has been executed by at least one test, though that doesn't prove every case is checked.
- A change can't be merged without tests, which slows small changes slightly.
- Coverage depends on deterministic tests. Tests whose coverage varies with timing must be made deterministic, as happened with the telemetry tests.
