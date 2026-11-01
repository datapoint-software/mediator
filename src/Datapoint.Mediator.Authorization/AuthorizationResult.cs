using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The combined decision of every authorizer that ran for one command, query or stream query.
/// </summary>
/// <remarks>
/// The action is allowed only when it has at least one authorizer and every authorizer allowed it. The authorizers run
/// in registration order and stop at the first denial, so <see cref="Decisions"/> holds every decision up to and
/// including the one that denied the action. When an authorizer throws, it holds the decisions made before, and the
/// result is denied.
/// </remarks>
public sealed class AuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class.
    /// </summary>
    /// <param name="action">The command, query or stream query that was authorized.</param>
    /// <param name="decisions">The decisions of the authorizers that ran, in the order they ran.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="action"/> or <paramref name="decisions"/> is <see langword="null"/>.
    /// </exception>
    public AuthorizationResult(object action, IReadOnlyList<AuthorizerDecision> decisions)
        : this(action, decisions, failed: false)
    {
    }

    // A failed authorization, where an authorizer threw, is denied whatever the decisions made before it.
    internal AuthorizationResult(object action, IReadOnlyList<AuthorizerDecision> decisions, bool failed)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(decisions);
        Action = action;
        Decisions = decisions;
        IsAllowed = !failed && decisions.Count > 0;

        foreach (var decision in decisions)
        {
            IsAllowed &= decision.Decision.IsAllowed;
        }
    }

    /// <summary>
    /// Gets the command, query or stream query that was authorized. It contains the caller's input, so don't log it as
    /// it is.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the type of the command, query or stream query.
    /// </summary>
    public Type ActionType => Action.GetType();

    /// <summary>
    /// Gets a value indicating whether the action is allowed: it has at least one authorizer, and all of them allowed
    /// it.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the decisions of the authorizers that ran, in the order they ran.
    /// </summary>
    public IReadOnlyList<AuthorizerDecision> Decisions { get; }

    /// <summary>
    /// Gets the details of the first decision whose details are a <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the details to find.</typeparam>
    /// <param name="details">The details, when found; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if a decision has details of type <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetDetails<T>([MaybeNullWhen(false)] out T details)
    {
        foreach (var decision in Decisions)
        {
            if (decision.Decision.Details is T match)
            {
                details = match;
                return true;
            }
        }

        details = default;
        return false;
    }
}
