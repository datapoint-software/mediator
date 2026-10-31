using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Handles a query.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TQueryResult">The type of the query's result.</typeparam>
/// <remarks>
/// Each query type must have exactly one handler.
/// </remarks>
public interface IQueryHandler<in TQuery, TQueryResult>
    where TQuery : IQuery<TQueryResult>
{
    /// <summary>
    /// Handles the specified query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes with the query's result. Await it only once.</returns>
    ValueTask<TQueryResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
