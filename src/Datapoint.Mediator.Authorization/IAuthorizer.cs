using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Decides whether a user may run a command, query or stream query, and why.
/// </summary>
/// <typeparam name="TAction">The type of the command, query or stream query.</typeparam>
/// <remarks>
/// An action can have several authorizers, and the user must be allowed by all of them. An action with no authorizer
/// is denied, unless it is marked with <see cref="AllowAnonymousAccessAttribute"/>. Authorizers only read: they must not
/// change data, because they can run inside the command's transaction.
/// </remarks>
public interface IAuthorizer<in TAction>
{
    /// <summary>
    /// Decides whether the user may run the specified action.
    /// </summary>
    /// <param name="action">The command, query or stream query to run.</param>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that completes with the decision, such as <see cref="AuthorizationDecision.Allow"/>,
    /// <see cref="AuthorizationDecision.Deny"/> or <see cref="AuthorizationDecision.AllowIf"/>. Await it only once.
    /// </returns>
    ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
