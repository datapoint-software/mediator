using System;

namespace Datapoint.Mediator;

/// <summary>
/// Describes the database transaction of the command, query or stream query being handled, as opened by a transaction
/// behaviour.
/// </summary>
/// <remarks>
/// <para>
/// A transaction behaviour, such as the one in <c>Datapoint.Mediator.EntityFrameworkCore</c>, implements and registers
/// it. Other behaviours, handlers and authorization observers read it without depending on that package, for example to
/// require a transaction or to record which attempt an audit entry belongs to.
/// </para>
/// <para>
/// The state flows with the asynchronous call: a request sent from a handler sees its own state while it runs, and the
/// outer request sees its own again afterwards. Parallel dispatches don't see each other's state.
/// </para>
/// </remarks>
public interface ITransactionContext
{
    /// <summary>
    /// Gets a value indicating whether the current request runs in a transaction opened by the mediator, either its own
    /// or one it joined.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction is read-only: it is rolled back, never saved.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the current request joined the transaction of an outer request, rather than
    /// opening its own.
    /// </summary>
    bool IsJoined { get; }

    /// <summary>
    /// Gets the attempt number of the transaction: 1 for the first attempt, then 2, 3 and so on when it is retried. It
    /// is 0 when no transaction is active.
    /// </summary>
    int Attempt { get; }

    /// <summary>
    /// Gets an identifier that is new for every attempt, or <see cref="Guid.Empty"/> when no transaction is active.
    /// </summary>
    Guid AttemptId { get; }
}
