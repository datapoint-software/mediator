using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace Datapoint.Mediator.FluentValidation.DependencyInjection.Tests;

public sealed record CreateUser(string Name) : ICommand;

public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

public sealed class CreateUserHandler(Recorder recorder) : ICommandHandler<CreateUser>
{
    public ValueTask HandleAsync(CreateUser command, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add(command.Name);
        return default;
    }
}

public sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator() => RuleFor(c => c.Name).NotEmpty();
}

public sealed class OtherBehavior : IBehavior;
