namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Provides the authorization decision of the command, query or stream query being handled.
/// </summary>
/// <remarks>
/// <para>
/// Inject it into handlers and behaviours that run after <see cref="AuthorizationBehavior"/>, to read the details of the
/// decision that allowed the action, such as the role and constraints that apply, instead of evaluating the rules again.
/// Handlers should use <see cref="GetResultFor"/> with the request they received, so they never read a decision made for another
/// request.
/// </para>
/// <para>
/// The decision flows with the asynchronous call, like <c>IHttpContextAccessor</c> in ASP.NET Core: a command sent from a
/// handler sees its own decision while it runs, and the outer decision again afterwards, and parallel dispatches don't
/// see each other's. Message handlers aren't authorized, so they see <see langword="null"/>, and so do actions marked
/// with <see cref="AllowAnonymousAccessAttribute"/>.
/// </para>
/// </remarks>
public interface IAuthorizationContext
{
    /// <summary>
    /// Gets the decision that allowed the current action, or <see langword="null"/> when it was not authorized.
    /// </summary>
    AuthorizationResult? Current { get; }

    /// <summary>
    /// Gets the decision that allowed <paramref name="action"/>, when it is the request being handled.
    /// </summary>
    /// <param name="action">The command, query or stream query the caller is handling.</param>
    /// <returns>
    /// The current decision, if it was made for this exact instance; otherwise, <see langword="null"/>. A behaviour that
    /// replaces the request with a new instance after authorization gets <see langword="null"/>, so a handler that
    /// requires a decision fails closed.
    /// </returns>
    AuthorizationResult? GetResultFor(object action);
}
