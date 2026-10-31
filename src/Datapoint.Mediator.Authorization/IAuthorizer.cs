using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Decides whether a user may run a command, query or stream query.
/// </summary>
/// <typeparam name="TAction">The type of the command, query or stream query.</typeparam>
/// <remarks>
/// An action can have several authorizers, and the user must be allowed by all of them. An action with no authorizer
/// is denied, unless it is marked with <see cref="AllowAnonymousAccessAttribute"/>.
/// </remarks>
public interface IAuthorizer<in TAction>
{
    /// <summary>
    /// Determines whether the user may run the specified action.
    /// </summary>
    /// <param name="action">The command, query or stream query to run.</param>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A task that completes with <see langword="true"/> if the user may run the action; otherwise,
    /// <see langword="false"/>. Await it only once.
    /// </returns>
    ValueTask<bool> IsAuthorizedAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default);
}
