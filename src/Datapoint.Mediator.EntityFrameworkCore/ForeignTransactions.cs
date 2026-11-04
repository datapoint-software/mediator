using System;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// What <see cref="TransactionBehavior{TDbContext}"/> does with a transaction that the mediator didn't begin.
/// </summary>
public enum ForeignTransactions
{
    /// <summary>
    /// The request joins the transaction, and whoever began it saves and commits it. The behaviour doesn't save, and
    /// applies no isolation level, initializers, read-only mode or retries.
    /// </summary>
    Join = 0,

    /// <summary>
    /// The request fails with an <see cref="InvalidOperationException"/>, so every transaction is begun by the mediator.
    /// </summary>
    Throw = 1,
}
