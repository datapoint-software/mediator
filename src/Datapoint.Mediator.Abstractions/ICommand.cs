namespace Datapoint.Mediator;

/// <summary>
/// Represents a command that changes state and produces no result.
/// </summary>
public interface ICommand : IAction;

/// <summary>
/// Represents a command that changes state and produces a result.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface ICommand<out TResult> : IAction;
