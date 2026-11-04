using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Observes every authorization of a command, query or stream query, allowed or not, for example to write an audit log.
/// </summary>
/// <remarks>
/// <para>
/// Every registered observer is called, in registration order, before the action runs or the authorization exception
/// is thrown. Actions marked with <see cref="AllowAnonymousAccessAttribute"/> are not authorized, so they are not
/// observed.
/// </para>
/// <para>
/// If an observer throws, the action does not run and the observer's exception propagates, even when access was
/// allowed: an action that can't be audited doesn't run. When authorization runs inside a command's transaction, a
/// denial rolls the transaction back, so an observer that must keep denials writes them through its own connection.
/// When the transaction is retried, the action is authorized and observed again.
/// </para>
/// </remarks>
public interface IAuthorizationObserver
{
    /// <summary>
    /// Called after a command, query or stream query is authorized.
    /// </summary>
    /// <param name="authorization">What was authorized, for whom, and the outcome.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the observer is done. Await it only once.</returns>
    ValueTask OnAuthorizedAsync(AuthorizationEvent authorization, CancellationToken cancellationToken = default);
}
