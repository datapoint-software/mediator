using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Settings for <see cref="AuthorizationBehavior"/>.
/// </summary>
public sealed class AuthorizationBehaviorOptions
{
    private TransactionRequirement _requireTransaction = TransactionRequirement.None;

    /// <summary>
    /// Gets or sets which actions must be authorized inside a transaction begun by the mediator, as reported by
    /// <see cref="ITransactionContext"/>. The default is <see cref="TransactionRequirement.None"/>.
    /// </summary>
    /// <remarks>
    /// When an action of a required kind is authorized without an active transaction, an
    /// <see cref="InvalidOperationException"/> is thrown: the behaviours are registered in the wrong order, or
    /// <see cref="ITransactionContext"/> isn't registered. Registering the behaviour with this setting also adds a
    /// startup check that refuses authorized actions of a required kind marked <c>[NonTransactional]</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public TransactionRequirement RequireTransaction
    {
        get => _requireTransaction;
        set => _requireTransaction = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"AuthorizationBehaviorOptions.RequireTransaction must be one of {string.Join(", ", Enum.GetNames<TransactionRequirement>())}.");
    }

    internal AuthorizationBehaviorOptions Copy() => (AuthorizationBehaviorOptions)MemberwiseClone();
}
