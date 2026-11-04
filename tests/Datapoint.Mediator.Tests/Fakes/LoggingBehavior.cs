using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Tests.Fakes;

// Logs "<name>>" before and "<<name>" after calling next, for every kind of action.
public sealed class LoggingBehavior(string name, List<string> log) : IBehavior
{
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        log.Add($"{name}>");
        await next(cancellationToken);
        log.Add($"<{name}");
    }

    public async ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default)
    {
        log.Add($"{name}>");
        var result = await next(cancellationToken);
        log.Add($"<{name}");
        return result;
    }

    public async ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default)
    {
        log.Add($"{name}>");
        var result = await next(cancellationToken);
        log.Add($"<{name}");
        return result;
    }

    public async ValueTask HandleAsync(IMessage message, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        log.Add($"{name}>");
        await next(cancellationToken);
        log.Add($"<{name}");
    }

    public async IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        log.Add($"{name}>");

        await foreach (var result in next(cancellationToken))
        {
            yield return result;
        }

        log.Add($"<{name}");
    }
}
