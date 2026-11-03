# 31. Every action has a description

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

An action's description is what its callers read first: the OpenAPI summary of its endpoint, the help of its CLI command, and the text an AI agent reads to choose an MCP tool. It was an optional `Description` property of `[Action]`. An action without one became an endpoint without a summary, a command without help and a tool an agent had to guess about, and nothing told the developer.

Making it optional only for the API adapter was considered, because OpenAPI survives without summaries. It was rejected: the OpenAPI document is read by people and by client generators too, and a rule that depends on which adapters offer an action is harder to explain than one rule for all of them.

## Decision

- The description is the second, required argument of `[Action]`: `[Action("invoices.create", "Creates an invoice for a customer.")]`. Framework Design Guidelines put required values in constructor parameters.
- The catalog refuses an empty or whitespace description at startup ([0027](0027-startup-checks.md)), naming the action.
- `ActionDescriptor.Description` is never `null`, so adapters always write it.

## Consequences

- Every endpoint, command and tool is described, and a missing description is found when the application starts, not by a caller.
- Applications on `4.0.0-beta.4` or earlier move `Description = "..."` to the second argument, and describe the actions that had none.
- The quality of a description is still up to the developer; the check only refuses an empty one.
