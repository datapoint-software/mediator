using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The decision of one authorizer, as part of an <see cref="AuthorizationResult"/>.
/// </summary>
public sealed class AuthorizerDecision
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizerDecision"/> class.
    /// </summary>
    /// <param name="authorizerType">The type of the authorizer that made the decision.</param>
    /// <param name="decision">The decision.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="authorizerType"/> or <paramref name="decision"/> is <see langword="null"/>.
    /// </exception>
    public AuthorizerDecision(Type authorizerType, AuthorizationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(authorizerType);
        ArgumentNullException.ThrowIfNull(decision);
        AuthorizerType = authorizerType;
        Decision = decision;
    }

    /// <summary>
    /// Gets the type of the authorizer that made the decision.
    /// </summary>
    public Type AuthorizerType { get; }

    /// <summary>
    /// Gets the decision.
    /// </summary>
    public AuthorizationDecision Decision { get; }
}
