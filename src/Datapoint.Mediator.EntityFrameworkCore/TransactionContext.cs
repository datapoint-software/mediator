using System;
using System.Threading;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Provides the state of the transaction that <see cref="TransactionBehavior{TDbContext}"/> runs the current request
/// in, from the current asynchronous flow.
/// </summary>
/// <remarks>
/// It holds no state of its own, so one instance can be shared by the whole application.
/// </remarks>
public sealed class TransactionContext : ITransactionContext
{
    private static readonly AsyncLocal<TransactionState?> Value = new();

    /// <inheritdoc/>
    public bool IsActive => Value.Value is not null;

    /// <inheritdoc/>
    public bool IsReadOnly => Value.Value?.IsReadOnly ?? false;

    /// <inheritdoc/>
    public bool IsJoined => Value.Value?.IsJoined ?? false;

    /// <inheritdoc/>
    public int Attempt => Value.Value?.Attempt ?? 0;

    /// <inheritdoc/>
    public Guid AttemptId => Value.Value?.AttemptId ?? Guid.Empty;

    // Set by TransactionBehavior inside async methods, so a change flows into the rest of the pipeline and the caller
    // gets its own value back when the method returns.
    internal static TransactionState? State
    {
        get => Value.Value;
        set => Value.Value = value;
    }
}

// The transaction of one request. DbContext identifies the context whose transaction it is, so a behaviour for another
// context type doesn't mistake it for its own.
internal sealed record TransactionState(object DbContext, bool IsReadOnly, bool IsJoined, int Attempt, Guid AttemptId);
