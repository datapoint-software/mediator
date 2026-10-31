using System.Collections.Generic;
using System.Threading;

namespace Datapoint.Mediator;

/// <summary>
/// Handles a stream query.
/// </summary>
/// <typeparam name="TQuery">The type of the stream query.</typeparam>
/// <typeparam name="TResult">The type of each result.</typeparam>
/// <remarks>
/// Each stream query type must have exactly one handler. When implementing <see cref="HandleAsync"/> as an
/// <c>async</c> iterator, mark its <c>ct</c> parameter with <c>[EnumeratorCancellation]</c>, so that a token passed to
/// <c>WithCancellation</c> also reaches the handler.
/// </remarks>
public interface IStreamQueryHandler<in TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    /// <summary>
    /// Handles the specified stream query.
    /// </summary>
    /// <param name="query">The stream query to handle.</param>
    /// <param name="ct">A token to cancel the enumeration.</param>
    /// <returns>The query's results, produced as they are enumerated.</returns>
    IAsyncEnumerable<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
