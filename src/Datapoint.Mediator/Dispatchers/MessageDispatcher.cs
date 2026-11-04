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
            // Never null: Activator.CreateInstance returns null only for Nullable<T>, and this type is a class.
            static type => (MessageDispatcher)Activator.CreateInstance(typeof(MessageDispatcher<>).MakeGenericType(type))!);

    public abstract ValueTask DispatchAsync(IMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class MessageDispatcher<TMessage> : MessageDispatcher
    where TMessage : IMessage
{
    public override ValueTask DispatchAsync(IMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handlers = serviceProvider.GetAll<IMessageHandler<TMessage>>();
        var typedMessage = (TMessage)message;
        var behaviors = serviceProvider.GetAll<IBehavior>();

        if (behaviors.Length == 0)
        {
            return handlers.Length == 0 ? default : PublishAsync(handlers, typedMessage, cancellationToken);
        }

        return InvokePipelineAsync(behaviors, handlers, message, typedMessage, cancellationToken);
    }

    // Kept separate from DispatchAsync, so the closures are only allocated when there are behaviours.
    private static ValueTask InvokePipelineAsync(
        IBehavior[] behaviors,
        IMessageHandler<TMessage>[] handlers,
        IMessage message,
        TMessage typedMessage,
        CancellationToken cancellationToken)
    {
        Func<CancellationToken, ValueTask> next = c => PublishAsync(handlers, typedMessage, c);

        next = Pipeline.Wrap(behaviors, message, next, static (behavior, request, inner) => c => behavior.HandleAsync(request, inner, c));
        return next(cancellationToken);
    }

    // Runs the handlers one after another, in registration order, and stops at the first exception.
    private static async ValueTask PublishAsync(IMessageHandler<TMessage>[] handlers, TMessage message, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
