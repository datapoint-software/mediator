namespace Datapoint.Mediator;

/// <summary>
/// Represents a query that reads state without changing it.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IQuery<out TResult>;
