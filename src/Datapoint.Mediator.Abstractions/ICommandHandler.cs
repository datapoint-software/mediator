using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Handles a command that produces no result.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <remarks>
/// Each command type must have exactly one handler.
/// </remarks>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the specified command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the command is handled. Await it only once.</returns>
    ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a command that produces a result.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <remarks>
/// Each command type must have exactly one handler.
/// </remarks>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the specified command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the command's result. Await it only once.</returns>
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
