using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace Datapoint.Mediator.Benchmarks;

public sealed record Ping : ICommand;

public sealed record ValidatedPing(string Name) : ICommand;

public sealed record Add(int A, int B) : ICommand<int>;

public sealed record Echo(string Value) : IQuery<string>;

public sealed record Notified : IMessage;

public sealed record CountTo(int Count) : IStreamQuery<int>;

public sealed class PingHandler : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken cancellationToken = default) => default;
}

public sealed class ValidatedPingHandler : ICommandHandler<ValidatedPing>
{
    public ValueTask HandleAsync(ValidatedPing command, CancellationToken cancellationToken = default) => default;
}

public sealed class ValidatedPingValidator : AbstractValidator<ValidatedPing>
{
    public ValidatedPingValidator() => RuleFor(c => c.Name).NotEmpty();
}

public sealed class AddHandler : ICommandHandler<Add, int>
{
    public ValueTask<int> HandleAsync(Add command, CancellationToken cancellationToken = default) => new(command.A + command.B);
}

public sealed class EchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken cancellationToken = default) => new(query.Value);
}

public sealed class CountToHandler : IStreamQueryHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> HandleAsync(CountTo query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        for (var i = 1; i <= query.Count; i++)
        {
            yield return i;
        }
    }
}

public sealed class FirstNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default) => default;
}

public sealed class SecondNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default) => default;
}

public sealed class ThirdNotifiedHandler : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default) => default;
}

// Two distinct types, because registering the same behaviour twice has no effect.
public sealed class FirstPassThroughBehavior : IBehavior
{
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default) =>
        next(cancellationToken);
}

public sealed class SecondPassThroughBehavior : IBehavior
{
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default) =>
        next(cancellationToken);
}
