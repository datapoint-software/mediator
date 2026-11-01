using System.Threading;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Provides the authorization decision of the command, query or stream query being handled, from the current
/// asynchronous flow.
/// </summary>
/// <remarks>
/// It holds no state of its own, so one instance can be shared by the whole application.
/// </remarks>
public sealed class AuthorizationContext : IAuthorizationContext
{
    private static readonly AsyncLocal<AuthorizationResult?> Value = new();

    /// <inheritdoc/>
    public AuthorizationResult? Current => Value.Value;

    /// <inheritdoc/>
    public AuthorizationResult? For(object action) =>
        Value.Value is { } result && ReferenceEquals(result.Action, action) ? result : null;

    // Set by AuthorizationBehavior. A change made inside an async method doesn't flow back to its caller.
    internal static AuthorizationResult? Result
    {
        get => Value.Value;
        set => Value.Value = value;
    }
}
