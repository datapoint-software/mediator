using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Datapoint.Mediator.Dispatchers;

internal abstract class StreamQueryDispatcher<TResult>
{
    private static readonly ConcurrentDictionary<Type, StreamQueryDispatcher<TResult>> Cache = new();

    public static StreamQueryDispatcher<TResult> Get(Type queryType) =>
        Cache.GetOrAdd(
            queryType,
            static type => (StreamQueryDispatcher<TResult>)Activator.CreateInstance(
                typeof(StreamQueryDispatcher<,>).MakeGenericType(type, typeof(TResult)))!);

    public abstract IAsyncEnumerable<TResult> Dispatch(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct);
}

internal sealed class StreamQueryDispatcher<TQuery, TResult> : StreamQueryDispatcher<TResult>
    where TQuery : IStreamQuery<TResult>
{
    public override IAsyncEnumerable<TResult> Dispatch(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        var handler = serviceProvider.GetRequiredHandler<IStreamQueryHandler<TQuery, TResult>>(typeof(TQuery));
        var typedQuery = (TQuery)query;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedQuery, ct);
        }

        return InvokePipeline(behaviors, handler, query, typedQuery, ct);
    }

    // Kept separate from Dispatch, so the closures are only allocated when there are behaviours.
    private static IAsyncEnumerable<TResult> InvokePipeline(
        IBehavior[] behaviors,
        IStreamQueryHandler<TQuery, TResult> handler,
        IStreamQuery<TResult> query,
        TQuery typedQuery,
        CancellationToken ct)
    {
        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, IAsyncEnumerable<TResult>> next = c => handler.HandleAsync(typedQuery, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(query, inner, c);
        }

        return next(ct);
    }
}
