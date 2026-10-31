using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Dispatchers;

internal abstract class MessageDispatcher
{
    private static readonly ConcurrentDictionary<Type, MessageDispatcher> Cache = new();

    public static MessageDispatcher Get(Type messageType) =>
        Cache.GetOrAdd(
            messageType,
            static type => (MessageDispatcher)Activator.CreateInstance(typeof(MessageDispatcher<>).MakeGenericType(type))!);

    public abstract ValueTask DispatchAsync(IMessage message, IServiceProvider serviceProvider, CancellationToken ct);
}

internal sealed class MessageDispatcher<TMessage> : MessageDispatcher
    where TMessage : IMessage
{
    public override ValueTask DispatchAsync(IMessage message, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var handlers = serviceProvider.GetAll<IMessageHandler<TMessage>>();
        var typedMessage = (TMessage)message;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handlers.Length == 0 ? default : PublishAsync(handlers, typedMessage, ct);
        }

        // Built from the innermost behaviour outwards, so the first registered behaviour runs first.
        Func<CancellationToken, ValueTask> next = c => PublishAsync(handlers, typedMessage, c);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = c => behavior.HandleAsync(message, inner, c);
        }

        return next(ct);
    }

    // Runs the handlers one after another, in registration order, and stops at the first exception.
    private static async ValueTask PublishAsync(IMessageHandler<TMessage>[] handlers, TMessage message, CancellationToken ct)
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(message, ct).ConfigureAwait(false);
        }
    }
}
