using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Dispatchers;

// Sends an action whose type is only known at runtime through IMediator. Created once per action type and cached.
internal abstract class ActionInvoker
{
    private static readonly ConcurrentDictionary<Type, ActionInvoker> Cache = new();

    public static ActionInvoker Get(ActionDescriptor action) =>
        Cache.GetOrAdd(action.ActionType, static (_, action) => Create(action), action);

    public virtual ValueTask<object?> InvokeAsync(IMediator mediator, object action, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Use StreamAsync to dispatch a stream query.");

    public virtual IAsyncEnumerable<object?> StreamAsync(IMediator mediator, object action, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Use DispatchAsync to dispatch a command or query.");

    private static ActionInvoker Create(ActionDescriptor action)
    {
        var invokerType = action.Kind switch
        {
            ActionKind.Command => typeof(CommandInvoker),
            ActionKind.CommandWithResult => typeof(CommandInvoker<>).MakeGenericType(action.ResultType!),
            ActionKind.Query => typeof(QueryInvoker<>).MakeGenericType(action.ResultType!),
            _ => typeof(StreamQueryInvoker<>).MakeGenericType(action.ResultType!),
        };

        return (ActionInvoker)Activator.CreateInstance(invokerType)!;
    }

    private sealed class CommandInvoker : ActionInvoker
    {
        public override async ValueTask<object?> InvokeAsync(IMediator mediator, object action, CancellationToken cancellationToken)
        {
            await mediator.SendAsync((ICommand)action, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private sealed class CommandInvoker<TResult> : ActionInvoker
    {
        public override async ValueTask<object?> InvokeAsync(IMediator mediator, object action, CancellationToken cancellationToken) =>
            await mediator.SendAsync((ICommand<TResult>)action, cancellationToken).ConfigureAwait(false);
    }

    private sealed class QueryInvoker<TResult> : ActionInvoker
    {
        public override async ValueTask<object?> InvokeAsync(IMediator mediator, object action, CancellationToken cancellationToken) =>
            await mediator.QueryAsync((IQuery<TResult>)action, cancellationToken).ConfigureAwait(false);
    }

    private sealed class StreamQueryInvoker<TResult> : ActionInvoker
    {
        public override IAsyncEnumerable<object?> StreamAsync(IMediator mediator, object action, CancellationToken cancellationToken) =>
            Box(mediator.StreamAsync((IStreamQuery<TResult>)action, cancellationToken), cancellationToken);

        private static async IAsyncEnumerable<object?> Box(
            IAsyncEnumerable<TResult> results,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var result in results.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return result;
            }
        }
    }
}
