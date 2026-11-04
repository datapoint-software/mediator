# Validation

`Datapoint.Mediator.FluentValidation` validates commands and queries with [FluentValidation](https://docs.fluentvalidation.net/) before their handler runs.

```bash
dotnet add package Datapoint.Mediator.FluentValidation.DependencyInjection --prerelease
```

## Validators

Write a validator per request type, in your business layer:

```csharp
public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoice>
{
    public CreateInvoiceValidator()
    {
        RuleFor(c => c.Customer).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Amount).GreaterThan(0);
    }
}
```

Register the validators with FluentValidation's own method, and add the behaviour:

```csharp
services.AddValidatorsFromAssemblyContaining<CreateInvoiceValidator>();
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()
    .AddValidationBehavior());
```

## How it behaves

- Every `IValidator<T>` registered for the request's **exact** type runs, one after another, asynchronously.
- If any fails, the behaviour throws `FluentValidation.ValidationException` with every failure, and the handler doesn't run.
- A request without validators passes.
- Commands, queries and stream queries are validated. Messages aren't, because your own code builds them.
- The behaviour is always scoped, so validators can use scoped services, such as a `DbContext` to check that a customer exists.

## Reporting errors safely

> **Security:** each failure's `AttemptedValue` is whatever the caller sent, which can be a password, personal data or an attack payload. Never return it, and don't log validation exceptions as they are. Report only `PropertyName` and `ErrorMessage`.
>
> Some default messages contain the value too: `InclusiveBetween` and `ExclusiveBetween` end with "You entered {PropertyValue}.", and translations may add it elsewhere. Give those rules your own message with `WithMessage`, and never use `{PropertyValue}` in one:
>
> ```csharp
> RuleFor(q => q.Max).InclusiveBetween(1, 100).WithMessage("'Max' must be between 1 and 100.");
> ```

Each host has a hook for this:

```csharp
// ASP.NET Core: a 400 with each property's errors
app.MapMediatorActions("/api", o => o.MapException = e => e is ValidationException v
    ? Results.ValidationProblem(v.Errors
        .GroupBy(f => f.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()))
    : null);

// CLI and MCP: one line of text
static string Describe(ValidationException v) =>
    string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
```

See the hosts' guides: [ASP.NET Core](hosts/aspnetcore.md#errors), [command line](hosts/command-line.md#errors), [MCP](hosts/model-context-protocol.md#errors).

## Messages in other languages

FluentValidation translates its default messages using the current UI culture, so a server running in Portuguese returns Portuguese messages. To fix the language, set it once at startup:

```csharp
ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");
```
