# Datapoint.Mediator.FluentValidation

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that validates commands and queries with [FluentValidation](https://docs.fluentvalidation.net/) before their handler runs.

Register it with [Datapoint.Mediator.FluentValidation.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.FluentValidation.DependencyInjection).

- Every `IValidator<T>` registered for the command or query's exact type runs, one after another.
- If any of them fails, a `FluentValidation.ValidationException` with all failures is thrown, and the handler doesn't run.
- Messages are not validated.

> **Security:** each failure carries the submitted value in `AttemptedValue`, which can contain passwords or personal data. Don't log validation exceptions as they are or return them to clients unchanged; include only `PropertyName` and `ErrorMessage` in error responses.
