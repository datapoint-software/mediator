using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Dispatchers;

internal abstract class ResultCommandDispatcher<TResult>
{
    private static readonly ConcurrentDictionary<Type, ResultCommandDispatcher<TResult>> Cache = new();

    public static ResultCommandDispatcher<TResult> Get(Type commandType) =>
        Cache.GetOrAdd(
            commandType,
            static type => (ResultCommandDispatcher<TResult>)Activator.CreateInstance(
                typeof(ResultCommandDispatcher<,>).MakeGenericType(type, typeof(TResult)))!);

    public abstract ValueTask<TResult> DispatchAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class ResultCommandDispatcher<TCommand, TResult> : ResultCommandDispatcher<TResult>
    where TCommand : ICommand<TResult>
{
    public override ValueTask<TResult> DispatchAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredHandler<ICommandHandler<TCommand, TResult>>(typeof(TCommand));
        var typedCommand = (TCommand)command;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedCommand, cancellationToken);
        }

        return InvokePipelineAsync(behaviors, handler, command, typedCommand, cancellationToken);
    }

    // Kept separate from DispatchAsync, so the closures are only allocated when there are behaviours.
    private static ValueTask<TResult> InvokePipelineAsync(
        IBehavior[] behaviors,
        ICommandHandler<TCommand, TResult> handler,
        ICommand<TResult> command,
        TCommand typedCommand,
        CancellationToken cancellationToken)
    {
        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, ValueTask<TResult>> next = c => handler.HandleAsync(typedCommand, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(command, inner, c);
        }

        return next(cancellationToken);
    }
}
