using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Dispatchers;

internal abstract class QueryDispatcher<TResult>
{
    private static readonly ConcurrentDictionary<Type, QueryDispatcher<TResult>> Cache = new();

    public static QueryDispatcher<TResult> Get(Type queryType) =>
        Cache.GetOrAdd(
            queryType,
            // Never null: Activator.CreateInstance returns null only for Nullable<T>, and this type is a class.
            static type => (QueryDispatcher<TResult>)Activator.CreateInstance(
                typeof(QueryDispatcher<,>).MakeGenericType(type, typeof(TResult)))!);

    public abstract ValueTask<TResult> DispatchAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class QueryDispatcher<TQuery, TResult> : QueryDispatcher<TResult>
    where TQuery : IQuery<TResult>
{
    public override ValueTask<TResult> DispatchAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredHandler<IQueryHandler<TQuery, TResult>>(typeof(TQuery));
        var typedQuery = (TQuery)query;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedQuery, cancellationToken);
        }

        return InvokePipelineAsync(behaviors, handler, query, typedQuery, cancellationToken);
    }

    // Kept separate from DispatchAsync, so the closures are only allocated when there are behaviours.
    private static ValueTask<TResult> InvokePipelineAsync(
        IBehavior[] behaviors,
        IQueryHandler<TQuery, TResult> handler,
        IQuery<TResult> query,
        TQuery typedQuery,
        CancellationToken cancellationToken)
    {
        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, ValueTask<TResult>> next = c => handler.HandleAsync(typedQuery, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(query, inner, c);
        }

        return next(cancellationToken);
    }
}
