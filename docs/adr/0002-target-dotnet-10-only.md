# 2. Target .NET 10 only

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

Multi-targeting, for example also targeting .NET 8 or .NET Standard 2.0, widens the audience, but it multiplies builds and tests, and blocks newer language and runtime features such as `GeneratedRegex` and default interface methods everywhere. The packages are written for Datapoint's own projects, which run on current .NET.

## Decision

Every package targets `net10.0` only. The library is versioned independently of .NET: adding a target framework is a minor version, and dropping one is a major version.

## Consequences

- One build and one test run, and the latest APIs can be used freely.
- Apps on earlier .NET versions can't use the packages.
- When .NET 10 reaches end of support, moving to a later target drops `net10.0`, which is a major version, unless both are targeted for a while.
