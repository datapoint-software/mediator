using System;
using System.Data;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Settings for <see cref="TransactionBehavior{TDbContext}"/>.
/// </summary>
/// <remarks>
/// The defaults keep the behaviour as it is without settings: commands run in transactions at the database's default
/// isolation level, queries don't run in transactions, and requests join transactions opened by the application.
/// </remarks>
public sealed class TransactionBehaviorOptions
{
    private IsolationLevel _isolationLevel = IsolationLevel.Unspecified;
    private QueryTransactions _queries = QueryTransactions.None;
    private IsolationLevel _queryIsolationLevel = IsolationLevel.Unspecified;
    private string? _readOnlyStatement;
    private ForeignTransactions _foreignTransactions = ForeignTransactions.Join;

    /// <summary>
    /// Gets or sets the isolation level of command transactions. The default, <see cref="IsolationLevel.Unspecified"/>,
    /// uses the database's default. Any other level requires a relational database provider.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public IsolationLevel IsolationLevel
    {
        get => _isolationLevel;
        set => _isolationLevel = Defined(value);
    }

    /// <summary>
    /// Gets or sets whether queries and stream queries run in transactions. The default is
    /// <see cref="QueryTransactions.None"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public QueryTransactions Queries
    {
        get => _queries;
        set => _queries = Defined(value);
    }

    /// <summary>
    /// Gets or sets the isolation level of query transactions, such as <see cref="IsolationLevel.RepeatableRead"/> for
    /// one snapshot per query on PostgreSQL. The default, <see cref="IsolationLevel.Unspecified"/>, uses the database's
    /// default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public IsolationLevel QueryIsolationLevel
    {
        get => _queryIsolationLevel;
        set => _queryIsolationLevel = Defined(value);
    }

    /// <summary>
    /// Gets or sets a SQL statement to run first in every read-only transaction, right after it begins and before any
    /// <see cref="ITransactionInitializer{TDbContext}"/>, such as <c>SET TRANSACTION READ ONLY</c> on PostgreSQL. The
    /// default, <see langword="null"/>, runs none: read-only transactions are then only rolled back.
    /// </summary>
    /// <remarks>
    /// The statement is a constant of the application. Never build it from input.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty or only white space.</exception>
    public string? ReadOnlyStatement
    {
        get => _readOnlyStatement;
        set => _readOnlyStatement = value is null || !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("The read-only statement must not be empty.", nameof(value));
    }

    /// <summary>
    /// Gets or sets what a request does when it finds a transaction that the mediator didn't begin, such as one the
    /// application began itself. The default is <see cref="EntityFrameworkCore.ForeignTransactions.Join"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public ForeignTransactions ForeignTransactions
    {
        get => _foreignTransactions;
        set => _foreignTransactions = Defined(value);
    }

    // A copy, so changing these options after they're passed to the behaviour has no effect.
    internal TransactionBehaviorOptions Copy() => (TransactionBehaviorOptions)MemberwiseClone();

    private static T Defined<T>(T value)
        where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not defined.");
}

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
