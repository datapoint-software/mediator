namespace Datapoint.Mediator.Authorization;

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
