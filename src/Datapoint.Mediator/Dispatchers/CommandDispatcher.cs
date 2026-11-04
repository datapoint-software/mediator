using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Dispatchers;

internal abstract class CommandDispatcher
{
    private static readonly ConcurrentDictionary<Type, CommandDispatcher> Cache = new();

    public static CommandDispatcher Get(Type commandType) =>
        Cache.GetOrAdd(
            commandType,
            // Never null: Activator.CreateInstance returns null only for Nullable<T>, and this type is a class.
            static type => (CommandDispatcher)Activator.CreateInstance(typeof(CommandDispatcher<>).MakeGenericType(type))!);

    public abstract ValueTask DispatchAsync(ICommand command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class CommandDispatcher<TCommand> : CommandDispatcher
    where TCommand : ICommand
{
    public override ValueTask DispatchAsync(ICommand command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredHandler<ICommandHandler<TCommand>>(typeof(TCommand));
        var typedCommand = (TCommand)command;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedCommand, cancellationToken);
        }

        return InvokePipelineAsync(behaviors, handler, command, typedCommand, cancellationToken);
    }

    // Kept separate from DispatchAsync, so the closures are only allocated when there are behaviours.
    private static ValueTask InvokePipelineAsync(
        IBehavior[] behaviors,
        ICommandHandler<TCommand> handler,
        ICommand command,
        TCommand typedCommand,
        CancellationToken cancellationToken)
    {
        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, ValueTask> next = c => handler.HandleAsync(typedCommand, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(command, inner, c);
        }

        return next(cancellationToken);
    }
}
