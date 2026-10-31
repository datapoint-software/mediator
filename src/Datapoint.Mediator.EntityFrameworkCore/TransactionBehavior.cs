using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Runs each command in a <typeparamref name="TDbContext"/> transaction, and saves its changes once, after the handler
/// succeeds.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <remarks>
/// <para>
/// For each command, the behaviour begins a transaction, runs the handler, calls
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> and commits. If anything fails, the transaction is rolled
/// back and nothing is saved. Handlers should therefore not call <c>SaveChangesAsync</c> themselves.
/// </para>
/// <para>
/// Queries, stream queries and messages are not affected. Commands marked with
/// <see cref="NonTransactionalAttribute"/> are not run in a transaction. A command sent from inside another command's
/// handler joins the outer command's transaction, and its changes are saved and committed with the outer command.
/// Messages published from a command's handler also run inside its transaction.
/// </para>
/// <para>
/// The transaction runs through the context's execution strategy. With a retrying strategy, such as
/// <c>EnableRetryOnFailure</c>, a transient failure runs the <b>whole command again, handler included</b>, after
/// clearing the change tracker. Handlers must then be safe to run more than once: perform side effects that cannot be
/// repeated, such as sending emails, in <see cref="NonTransactionalAttribute"/> commands or after the commit.
/// </para>
/// <para>
/// Register this behaviour after authorization and validation, so that a transaction is only opened for commands that
/// are allowed and valid.
/// </para>
/// </remarks>
public sealed class TransactionBehavior<TDbContext> : IBehavior
    where TDbContext : DbContext
{
    private static readonly ConcurrentDictionary<Type, bool> NonTransactionalCommands = new();

    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionBehavior{TDbContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context to run commands in a transaction of.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public TransactionBehavior(TDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        RunsWithoutTransaction(command) ? next(ct) : RunInTransactionAsync(next, ct);

    /// <inheritdoc/>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default) =>
        RunsWithoutTransaction(command) ? next(ct) : RunInTransactionAsync(next, ct);

    // A command already inside a transaction, such as one sent from another command's handler, joins it.
    private bool RunsWithoutTransaction(object command) =>
        _dbContext.Database.CurrentTransaction is not null
        || NonTransactionalCommands.GetOrAdd(
            command.GetType(),
            static type => type.IsDefined(typeof(NonTransactionalAttribute), inherit: false));

    private async ValueTask RunInTransactionAsync(Func<CancellationToken, ValueTask> next, CancellationToken ct) =>
        await RunInTransactionAsync<bool>(
            async c =>
            {
                await next(c).ConfigureAwait(false);
                return true;
            },
            ct).ConfigureAwait(false);

    private async ValueTask<TResult> RunInTransactionAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> next, CancellationToken ct)
    {
        var attempt = 0;
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async c =>
            {
                // A retry starts from a clean change tracker, so changes from the failed attempt are not saved twice.
                if (attempt++ > 0)
                {
                    _dbContext.ChangeTracker.Clear();
                }

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(c).ConfigureAwait(false);
                var result = await next(c).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(c).ConfigureAwait(false);
                await transaction.CommitAsync(c).ConfigureAwait(false);
                return result;
            },
            ct).ConfigureAwait(false);
    }
}
