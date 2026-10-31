using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace Datapoint.Mediator.Benchmarks;

public sealed record Ping : ICommand;

public sealed record ValidatedPing(string Name) : ICommand;

public sealed record Add(int A, int B) : ICommand<int>;

public sealed record Echo(string Value) : IQuery<string>;

public sealed record Notified : IMessage;

public sealed class PingHandler : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken ct = default) => default;
}

public sealed class ValidatedPingHandler : ICommandHandler<ValidatedPing>
{
    public ValueTask HandleAsync(ValidatedPing command, CancellationToken ct = default) => default;
}

public sealed class ValidatedPingValidator : AbstractValidator<ValidatedPing>
{
    public ValidatedPingValidator() => RuleFor(c => c.Name).NotEmpty();
}

public sealed class AddHandler : ICommandHandler<Add, int>
{
    public ValueTask<int> HandleAsync(Add command, CancellationToken ct = default) => new(command.A + command.B);
}

public sealed class EchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken ct = default) => new(query.Value);
}

public sealed class FirstNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken ct = default) => default;
}

public sealed class SecondNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken ct = default) => default;
}

public sealed class ThirdNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken ct = default) => default;
}

// Two distinct types, because registering the same behaviour twice has no effect.
public sealed class FirstPassThroughBehavior : IBehavior
{
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        next(ct);
}

public sealed class SecondPassThroughBehavior : IBehavior
{
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        next(ct);
}
