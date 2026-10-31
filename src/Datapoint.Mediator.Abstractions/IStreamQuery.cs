namespace Datapoint.Mediator;

/// <summary>
/// Represents a query that reads state without changing it, and produces its results as a stream.
/// </summary>
/// <typeparam name="TResult">The type of each result.</typeparam>
/// <remarks>
/// Use a stream query for large result sets, so that results can be processed as they arrive instead of being loaded
/// into memory all at once.
/// </remarks>
public interface IStreamQuery<out TResult>;
