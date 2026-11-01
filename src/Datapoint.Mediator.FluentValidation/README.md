# Datapoint.Mediator.FluentValidation

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that validates commands, queries and stream queries with [FluentValidation](https://docs.fluentvalidation.net/) before their handler runs, and throws `ValidationException` when they're invalid. Register it with [Datapoint.Mediator.FluentValidation.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.FluentValidation.DependencyInjection).

> **Security:** each failure's `AttemptedValue` is whatever the caller sent. Never return it or log it: report only `PropertyName` and `ErrorMessage`.

**Documentation:** [Validation](https://github.com/datapoint-software/mediator/blob/main/docs/validation.md) · [Manual](https://github.com/datapoint-software/mediator/blob/main/docs/README.md)
