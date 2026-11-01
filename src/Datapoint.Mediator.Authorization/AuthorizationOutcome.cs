namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The outcome of authorizing a command, query or stream query.
/// </summary>
public enum AuthorizationOutcome
{
    /// <summary>
    /// Every authorizer allowed the action, which runs.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// An authorizer denied the action, or it has no authorizer. An <see cref="AccessDeniedException"/> is thrown.
    /// </summary>
    Denied = 1,

    /// <summary>
    /// There is no signed-in user. An <see cref="AuthenticationRequiredException"/> is thrown.
    /// </summary>
    AuthenticationRequired = 2,

    /// <summary>
    /// An authorizer threw an exception, which propagates. The action does not run.
    /// </summary>
    Failed = 3,
}
