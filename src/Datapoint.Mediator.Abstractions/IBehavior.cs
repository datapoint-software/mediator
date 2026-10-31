using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Wraps the handling of commands, queries and messages, to add cross-cutting behaviour such as auditing,
/// validation or exception handling.
/// </summary>
/// <remarks>
/// Every method has a default implementation that calls <c>next</c>, so a behaviour only implements the methods it needs.
/// A behaviour can run code before and after calling <c>next</c>, or skip calling it to short-circuit the pipeline.
/// </remarks>
public interface IBehavior
{
    /// <summary>
    /// Handles a command that produces no result.
    /// </summary>
    /// <param name="command">The command being handled.</param>
    /// <param name="next">Invokes the next behaviour in the pipeline, or the command's handler.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the command is handled. Await it only once.</returns>
    ValueTask HandleAsync(
        ICommand command,
        Func<CancellationToken, ValueTask> next,
        CancellationToken cancellationToken = default) =>
        next(cancellationToken);

    /// <summary>
    /// Handles a command that produces a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    /// <param name="command">The command being handled.</param>
    /// <param name="next">Invokes the next behaviour in the pipeline, or the command's handler.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the command's result. Await it only once.</returns>
    ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default) =>
        next(cancellationToken);

    /// <summary>
    /// Handles a query.
    /// </summary>
    /// <typeparam name="TResult">The type of the query's result.</typeparam>
    /// <param name="query">The query being handled.</param>
    /// <param name="next">Invokes the next behaviour in the pipeline, or the query's handler.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the query's result. Await it only once.</returns>
    ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default) =>
        next(cancellationToken);

    /// <summary>
    /// Handles a message being published.
    /// </summary>
    /// <remarks>
    /// Runs once per publish, around all of the message's handlers together.
    /// </remarks>
    /// <param name="message">The message being published.</param>
    /// <param name="next">Invokes the next behaviour in the pipeline, or all of the message's handlers.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when every handler has handled the message. Await it only once.</returns>
    ValueTask HandleAsync(
        IMessage message,
        Func<CancellationToken, ValueTask> next,
        CancellationToken cancellationToken = default) =>
        next(cancellationToken);
}
