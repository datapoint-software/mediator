using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public ValueTask HandleAsync(Ping command, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("ping");
        return default;
    }
}

public sealed class OtherPingHandler : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken cancellationToken = default) => default;
}

public sealed class AddHandler : ICommandHandler<Add, int>
{
    public ValueTask<int> HandleAsync(Add command, CancellationToken cancellationToken = default) => new(command.A + command.B);
}

public sealed class EchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken cancellationToken = default) => new(query.Value);
}

public sealed class OtherEchoHandler : IQueryHandler<Echo, string>
{
    public ValueTask<string> HandleAsync(Echo query, CancellationToken cancellationToken = default) => new(query.Value);
}

public sealed class FirstNotifiedHandler(Recorder recorder) : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("first");
        return default;
    }
}

public sealed class SecondNotifiedHandler(Recorder recorder) : IMessageHandler<Notified>
{
    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("second");
        return default;
    }
}

public sealed class MultiHandler : ICommandHandler<MultiCommand>, IQueryHandler<MultiQuery, int>, IMessageHandler<Notified>
{
    public ValueTask HandleAsync(MultiCommand command, CancellationToken cancellationToken = default) => default;

    public ValueTask<int> HandleAsync(MultiQuery query, CancellationToken cancellationToken = default) => new(0);

    public ValueTask HandleAsync(Notified message, CancellationToken cancellationToken = default) => default;
}

public abstract class AbstractHandler : ICommandHandler<Ping>
{
    public abstract ValueTask HandleAsync(Ping command, CancellationToken cancellationToken = default);
}

public sealed class GenericHandler<T> : ICommandHandler<Ping>
{
    public ValueTask HandleAsync(Ping command, CancellationToken cancellationToken = default) => default;
}

public sealed class NotAHandler;

public sealed class OuterBehavior(Recorder recorder) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("outer>");
        await next(cancellationToken);
        recorder.Log.Add("<outer");
    }
}

public sealed class InnerBehavior(Recorder recorder) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        recorder.Log.Add("inner>");
        await next(cancellationToken);
        recorder.Log.Add("<inner");
    }
}

public abstract class AbstractBehavior : IBehavior;

public sealed record CountTo(int Count) : IStreamQuery<int>;

public sealed class CountToHandler : IStreamQueryHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> HandleAsync(CountTo query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= query.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

public sealed class OtherCountToHandler : IStreamQueryHandler<CountTo, int>
{
    public IAsyncEnumerable<int> HandleAsync(CountTo query, CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<int>();
}
