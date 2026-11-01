using System;
using System.Security.Claims;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Describes one authorization of a command, query or stream query, for an <see cref="IAuthorizationObserver"/>.
/// </summary>
public sealed class AuthorizationEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationEvent"/> class.
    /// </summary>
    /// <param name="action">The command, query or stream query.</param>
    /// <param name="principal">The current user, or <see langword="null"/> when there is none.</param>
    /// <param name="outcome">The outcome.</param>
    /// <param name="result">The combined decision, or <see langword="null"/> when no authorizer was asked.</param>
    /// <param name="exception">The exception an authorizer threw, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public AuthorizationEvent(
        object action,
        ClaimsPrincipal? principal,
        AuthorizationOutcome outcome,
        AuthorizationResult? result,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
        Principal = principal;
        Outcome = outcome;
        Result = result;
        Exception = exception;
    }

    /// <summary>
    /// Gets the command, query or stream query. It contains the caller's input, so don't log it as it is.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the current user, or <see langword="null"/> when there is none.
    /// </summary>
    public ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public AuthorizationOutcome Outcome { get; }

    /// <summary>
    /// Gets the combined decision, with each authorizer's details. It is <see langword="null"/> when no authorizer
    /// was asked (<see cref="AuthorizationOutcome.AuthenticationRequired"/>). When the action has no authorizer, it has
    /// no decisions. When an authorizer threw (<see cref="AuthorizationOutcome.Failed"/>), it is denied and holds the
    /// decisions made before.
    /// </summary>
    public AuthorizationResult? Result { get; }

    /// <summary>
    /// Gets the exception an authorizer threw, when the outcome is <see cref="AuthorizationOutcome.Failed"/>;
    /// otherwise, <see langword="null"/>.
    /// </summary>
    public Exception? Exception { get; }
}
