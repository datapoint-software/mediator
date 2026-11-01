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
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not defined.");
    }

    internal AuthorizationBehaviorOptions Copy() => (AuthorizationBehaviorOptions)MemberwiseClone();
}

/// <summary>
/// Which actions must be authorized inside a transaction.
/// </summary>
public enum TransactionRequirement
{
    /// <summary>
    /// No action needs a transaction to be authorized.
    /// </summary>
    None = 0,

    /// <summary>
    /// Commands must be authorized inside a transaction.
    /// </summary>
    Commands = 1,

    /// <summary>
    /// Commands, queries and stream queries must be authorized inside a transaction.
    /// </summary>
    CommandsAndQueries = 2,
}
