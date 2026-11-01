using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Handles a query.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResult">The type of the query's result.</typeparam>
/// <remarks>
/// Each query type must have exactly one handler.
/// </remarks>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the specified query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that completes with the query's result. Await it only once.</returns>
    ValueTask<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
