using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace Datapoint.Mediator.FluentValidation.Tests;

public sealed record CreateUser(string Name, string Email) : ICommand;

public sealed record AddNumbers(int A, int B) : ICommand<int>;

public sealed record FindUser(string Name) : IQuery<string>;

public sealed record UserCreated(string Name) : IMessage;

public sealed record ListUsers(string Filter) : IStreamQuery<string>;

public sealed class NameValidator : AbstractValidator<CreateUser>
{
    public NameValidator() => RuleFor(c => c.Name).NotEmpty();
}

public sealed class EmailValidator : AbstractValidator<CreateUser>
{
    public EmailValidator() => RuleFor(c => c.Email).NotEmpty();
}

public sealed class AddNumbersValidator : AbstractValidator<AddNumbers>
{
    public AddNumbersValidator() => RuleFor(c => c.A).GreaterThanOrEqualTo(0);
}

public sealed class FindUserValidator : AbstractValidator<FindUser>
{
    public FindUserValidator() => RuleFor(q => q.Name).NotEmpty();
}

public sealed class UserCreatedValidator : AbstractValidator<UserCreated>
{
    public UserCreatedValidator() => RuleFor(m => m.Name).NotEmpty();
}

public sealed class ListUsersValidator : AbstractValidator<ListUsers>
{
    public ListUsersValidator() => RuleFor(q => q.Filter).NotEmpty();
}

// Logs its name and the token it received, after an optional delay, and always passes.
public sealed class LoggingValidator : AbstractValidator<CreateUser>
{
    public LoggingValidator(string name, TimeSpan delay, List<string> log, List<CancellationToken> tokens) =>
        RuleFor(c => c).MustAsync(async (_, ct) =>
        {
            await Task.Delay(delay, ct);
            log.Add(name);
            tokens.Add(ct);
            return true;
        });
}

public sealed class ScopedDependency;

public sealed class ScopedDependencyValidator : AbstractValidator<CreateUser>
{
    public ScopedDependencyValidator(ScopedDependency dependency) => RuleFor(c => c.Name).NotEmpty();
}

public sealed class DelegateServiceProvider(Func<Type, object?> getService) : IServiceProvider
{
    public object? GetService(Type serviceType) => getService(serviceType);
}
