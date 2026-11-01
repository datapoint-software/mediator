using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The decision of one <see cref="IAuthorizer{TAction}"/>: whether the user may run an action, and why.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Details"/> carries an application-defined value that explains the decision, such as the share, role or
/// rule that applied, or the constraints an allowed action must respect. It is meant for the application: its handlers,
/// its behaviours and its audit log. It never reaches the caller: a denied action fails with an
/// <see cref="AccessDeniedException"/> whose message does not include it, and host adapters only report that access
/// was denied.
/// </para>
/// <para>
/// Don't put secrets in the details unless every reader of the audit log may see them.
/// </para>
/// </remarks>
public sealed class AuthorizationDecision
{
    private static readonly AuthorizationDecision Allowed = new(isAllowed: true, details: null);
    private static readonly AuthorizationDecision Denied = new(isAllowed: false, details: null);

    private AuthorizationDecision(bool isAllowed, object? details, Exception? exception = null)
    {
        IsAllowed = isAllowed;
        Details = details;
        Exception = exception;
    }

    /// <summary>
    /// Gets a value indicating whether the user may run the action.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the application-defined value that explains the decision, or <see langword="null"/> when there is none.
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// Gets the exception to throw instead of <see cref="AccessDeniedException"/> when this decision denies the action,
    /// or <see langword="null"/> to throw <see cref="AccessDeniedException"/>.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Creates a decision that allows the action.
    /// </summary>
    /// <param name="details">
    /// An application-defined value that explains why the action is allowed, or <see langword="null"/>.
    /// </param>
    /// <returns>A decision that allows the action.</returns>
    public static AuthorizationDecision Allow(object? details = null) =>
        details is null ? Allowed : new(isAllowed: true, details);

    /// <summary>
    /// Creates a decision that denies the action.
    /// </summary>
    /// <param name="details">
    /// An application-defined value that explains why the action is denied, or <see langword="null"/>. It is never
    /// reported to the caller.
    /// </param>
    /// <param name="exception">
    /// The exception to throw instead of <see cref="AccessDeniedException"/>, for an outcome of the application's own,
    /// such as "step-up authentication required"; or <see langword="null"/>. Host adapters treat it like any exception
    /// of the application: they report it only if the application maps it, so don't put details in its message unless
    /// that mapping is safe. When a transaction is retried, the same instance is thrown again.
    /// </param>
    /// <returns>A decision that denies the action.</returns>
    public static AuthorizationDecision Deny(object? details = null, Exception? exception = null) =>
        details is null && exception is null ? Denied : new(isAllowed: false, details, exception);

    /// <summary>
    /// Creates a decision without details that allows the action when <paramref name="condition"/> is
    /// <see langword="true"/>, and denies it otherwise.
    /// </summary>
    /// <param name="condition">Whether the user may run the action.</param>
    /// <returns>A decision that allows or denies the action.</returns>
    public static AuthorizationDecision AllowIf(bool condition) => condition ? Allowed : Denied;
}
