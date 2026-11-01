namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Provides the authorization decision of the command, query or stream query being handled.
/// </summary>
/// <remarks>
/// <para>
/// Inject it into handlers and behaviours that run after <see cref="AuthorizationBehavior"/>, to read the details of the
/// decision that allowed the action, such as the role and constraints that apply, instead of evaluating the rules again.
/// </para>
/// <para>
/// The decision flows with the asynchronous call, like <c>IHttpContextAccessor</c> in ASP.NET Core: a command sent from a
/// handler sees its own decision while it runs, and the outer decision again afterwards, and parallel dispatches don't
/// see each other's. Message handlers see the decision of the action that published the message. Actions marked with
/// <see cref="AllowAnonymousAccessAttribute"/> see <see langword="null"/>.
/// </para>
/// </remarks>
public interface IAuthorizationContext
{
    /// <summary>
    /// Gets the decision that allowed the current action, or <see langword="null"/> when it was not authorized.
    /// </summary>
    AuthorizationResult? Current { get; }
}
