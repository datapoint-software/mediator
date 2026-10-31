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
            static type => (CommandDispatcher)Activator.CreateInstance(typeof(CommandDispatcher<>).MakeGenericType(type))!);

    public abstract ValueTask DispatchAsync(ICommand command, IServiceProvider serviceProvider, CancellationToken ct);
}

internal sealed class CommandDispatcher<TCommand> : CommandDispatcher
    where TCommand : ICommand
{
    public override ValueTask DispatchAsync(ICommand command, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var handler = serviceProvider.GetRequiredHandler<ICommandHandler<TCommand>>(typeof(TCommand));
        var typedCommand = (TCommand)command;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedCommand, ct);
        }

        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, ValueTask> next = c => handler.HandleAsync(typedCommand, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(command, inner, c);
        }

        return next(ct);
    }
}
