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
            // Never null: Activator.CreateInstance returns null only for Nullable<T>, and this type is a class.
            static type => (StreamQueryDispatcher<TResult>)Activator.CreateInstance(
                typeof(StreamQueryDispatcher<,>).MakeGenericType(type, typeof(TResult)))!);

    public abstract IAsyncEnumerable<TResult> Dispatch(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class StreamQueryDispatcher<TQuery, TResult> : StreamQueryDispatcher<TResult>
    where TQuery : IStreamQuery<TResult>
{
    public override IAsyncEnumerable<TResult> Dispatch(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredHandler<IStreamQueryHandler<TQuery, TResult>>(typeof(TQuery));
        var typedQuery = (TQuery)query;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedQuery, cancellationToken);
        }

        return InvokePipeline(behaviors, handler, query, typedQuery, cancellationToken);
    }

    // Kept separate from Dispatch, so the closures are only allocated when there are behaviours.
    private static IAsyncEnumerable<TResult> InvokePipeline(
        IBehavior[] behaviors,
        IStreamQueryHandler<TQuery, TResult> handler,
        IStreamQuery<TResult> query,
        TQuery typedQuery,
        CancellationToken cancellationToken)
    {
        Func<CancellationToken, IAsyncEnumerable<TResult>> next = c => handler.HandleAsync(typedQuery, c);

        next = Pipeline.Wrap(behaviors, query, next, static (behavior, request, inner) => c => behavior.HandleAsync(request, inner, c));
        return next(cancellationToken);
    }
}
