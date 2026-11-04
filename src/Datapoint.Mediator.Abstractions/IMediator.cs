using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Dispatches commands and queries to their handler, and publishes messages to their handlers.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a command to its <see cref="ICommandHandler{TCommand}"/>.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the command is handled. Await it only once.</returns>
    ValueTask SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to its <see cref="ICommandHandler{TCommand, TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the command's result. Await it only once.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query to its <see cref="IQueryHandler{TQuery, TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the query's result.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the query's result. Await it only once.</returns>
    ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stream query to its <see cref="IStreamQueryHandler{TQuery, TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of each result.</typeparam>
    /// <param name="query">The stream query to send.</param>
    /// <param name="cancellationToken">A token to cancel the enumeration.</param>
    /// <returns>
    /// The query's results, produced as they are enumerated. The handler is resolved when this method is called, and
    /// runs when the results are enumerated.
    /// </returns>
    IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to every <see cref="IMessageHandler{TMessage}"/> registered for it.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when every handler has handled the message. Await it only once.</returns>
    ValueTask PublishAsync(IMessage message, CancellationToken cancellationToken = default);
}
