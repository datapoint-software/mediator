using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed record Ping : ICommand;

public sealed record Add(int A, int B) : ICommand<int>;

public sealed record Echo(string Value) : IQuery<string>;

public sealed record Notified : IMessage;

public sealed record MultiCommand : ICommand;

public sealed record MultiQuery : IQuery<int>;

// Shared log for the integration tests.
public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

public sealed class PingHandler(Recorder recorder) : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken ct = default)
    {
        recorder.Log.Add("ping");
        return default;
    }
}

public sealed class OtherPingHandler : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken ct = default) => default;
}

public sealed class AddHandler : ICommandHandler<Add, int>
{
    public ValueTask<int> HandleAsync(Add command, CancellationToken ct = default) => new(command.A + command.B);
}

public sealed class EchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken ct = default) => new(query.Value);
}

public sealed class OtherEchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken ct = default) => new(query.Value);
}

public sealed class FirstNotifiedHandler(Recorder recorder) : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken ct = default)
    {
        recorder.Log.Add("first");
        return default;
    }
}

public sealed class SecondNotifiedHandler(Recorder recorder) : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken ct = default)
    {
        recorder.Log.Add("second");
        return default;
    }
}

public sealed class MultiHandler : ICommandHandler<MultiCommand>, IQueryHandler<MultiQuery, int>, IMessageHandler<Notified>
{
    public ValueTask HandleAsync(MultiCommand command, CancellationToken ct = default) => default;

    public ValueTask<int> HandleAsync(MultiQuery query, CancellationToken ct = default) => new(0);

    public ValueTask HandleAsync(Notified message, CancellationToken ct = default) => default;
}

public abstract class AbstractHandler : ICommandHandler<Ping>
{
    public abstract ValueTask HandleAsync(Ping command, CancellationToken ct = default);
}

public sealed class GenericHandler<T> : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken ct = default) => default;
}

public sealed class NotAHandler;

public sealed class OuterBehavior(Recorder recorder) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default)
    {
        recorder.Log.Add("outer>");
        await next(ct);
        recorder.Log.Add("<outer");
    }
}

public sealed class InnerBehavior(Recorder recorder) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default)
    {
        recorder.Log.Add("inner>");
        await next(ct);
        recorder.Log.Add("<inner");
    }
}

public abstract class AbstractBehavior : IBehavior;
