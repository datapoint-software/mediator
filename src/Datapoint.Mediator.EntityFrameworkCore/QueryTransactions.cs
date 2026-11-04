namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Whether <see cref="TransactionBehavior{TDbContext}"/> runs queries and stream queries in transactions.
/// </summary>
public enum QueryTransactions
{
    /// <summary>
    /// Queries and stream queries don't run in transactions of their own.
    /// </summary>
    None = 0,

    /// <summary>
    /// Queries and stream queries run in read-only transactions, which are always rolled back and never saved.
    /// </summary>
    ReadOnly = 1,
}
