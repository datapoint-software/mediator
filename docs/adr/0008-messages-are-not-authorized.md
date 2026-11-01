# 8. Messages are not authorized

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

Messages, published with `PublishAsync`, notify other parts of the application that something happened, such as `InvoiceCancelled`. They are published by the application's own code, usually from a command's handler, after that command was authorized.

## Decision

- `AuthorizationBehavior` does not authorize messages.
- Messages can't be actions: marking a message with `[Action]` fails at startup, in `AddMediator`, so a message can never be dispatched by name from a host.

## Consequences

- Message handlers run with no authorization check, so **a message must never be built directly from user input**, for example in an endpoint. Send an authorized command instead, and publish the message from its handler. The authorization documentation and XML docs carry this warning.
- Message handlers don't need authorizers.
