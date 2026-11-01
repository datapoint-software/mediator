using System;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Describes a transaction being prepared by an <see cref="ITransactionInitializer{TDbContext}"/>.
/// </summary>
public sealed class TransactionInitialization
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInitialization"/> class.
    /// </summary>
    /// <param name="request">The command, query or stream query that runs in the transaction.</param>
    /// <param name="isReadOnly">Whether the transaction is read-only.</param>
    /// <param name="isJoined">Whether the request joined the transaction of an outer request.</param>
    /// <param name="attempt">The attempt number, from 1.</param>
    /// <param name="attemptId">The identifier of the attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public TransactionInitialization(object request, bool isReadOnly, bool isJoined, int attempt, Guid attemptId)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
        IsReadOnly = isReadOnly;
        IsJoined = isJoined;
        Attempt = attempt;
        AttemptId = attemptId;
    }

    /// <summary>
    /// Gets the command, query or stream query that runs in the transaction. It contains the caller's input, and isn't
    /// authorized yet.
    /// </summary>
    public object Request { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction is read-only, as for queries.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the request joined the transaction of an outer request, which was already
    /// prepared.
    /// </summary>
    public bool IsJoined { get; }

    /// <summary>
    /// Gets the attempt number of the transaction: 1, then 2, 3 and so on when it is retried.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets an identifier that is new for every attempt.
    /// </summary>
    public Guid AttemptId { get; }
}
