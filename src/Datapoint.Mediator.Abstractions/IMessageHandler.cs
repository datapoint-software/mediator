using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Handles a message.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <remarks>
/// A message type can have any number of handlers, and each one receives every published message.
/// </remarks>
public interface IMessageHandler<in TMessage>
    where TMessage : IMessage
{
    /// <summary>
    /// Handles the specified message.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that completes when the message is handled. Await it only once.</returns>
    ValueTask HandleAsync(TMessage message, CancellationToken ct = default);
}
