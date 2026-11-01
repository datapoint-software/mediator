# 1. Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

Many of this library's behaviours are deliberate trade-offs: authorization denies by default, messages run inside the caller's transaction, retries run a command again. Without a record, a future maintainer sees only the code, and may "fix" a decision without knowing why it was made, or reopen a discussion that was already settled.

## Decision

Significant decisions are recorded in `docs/adr/`, one file per decision, following [Michael Nygard's format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions). Records are never rewritten after they're accepted: a changed decision gets a new record that supersedes the old one.

A decision is significant when it affects the public API, security, data integrity or performance, or when reversing it later would be a breaking change.

## Consequences

- Maintainers read the relevant record before changing a decided behaviour, and a reversal is explicit and reviewed.
- Records 0002 to 0016 were written after the fact, from the discussions and git history of the first public version. Their dates are when each decision was taken.
