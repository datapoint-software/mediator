# Changelog

All notable changes to the Datapoint.Mediator packages are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the packages follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

## [4.0.0-alpha.1](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-alpha.1) - 2026-10-31

Initial public release.

### Added

- `Datapoint.Mediator.Abstractions`: commands, queries, messages, their handlers, behaviours and `IMediator`.
- `Datapoint.Mediator`: the mediator implementation.
- `Datapoint.Mediator.DependencyInjection`: `AddMediator`, with handler and behaviour registration and assembly scanning.
- `Datapoint.Mediator.FluentValidation`: a behaviour that validates commands and queries with FluentValidation.
- `Datapoint.Mediator.FluentValidation.DependencyInjection`: `AddValidationBehavior`.
