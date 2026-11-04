using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Runs each command in a <typeparamref name="TDbContext"/> transaction, and saves its changes once, after the handler
/// succeeds. Optionally, runs queries and stream queries in read-only transactions.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <remarks>
/// <para>
/// For each command, the behaviour begins a transaction, runs every <see cref="ITransactionInitializer{TDbContext}"/>,
/// runs the rest of the pipeline and the handler, calls <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> and
/// commits. If anything fails, the transaction is rolled back and nothing is saved. Handlers should therefore not call
/// <c>SaveChangesAsync</c> themselves.
/// </para>
/// <para>
/// With <see cref="TransactionBehaviorOptions.Queries"/> set to <see cref="QueryTransactions.ReadOnly"/>, queries and
/// stream queries run in transactions too, which are always rolled back: nothing a query changes is saved. A stream
/// query's transaction begins when its results are first enumerated, and ends when the enumeration completes, fails or
/// is stopped, so it holds a connection that long. Stream queries can't run in a transaction with a retrying execution
/// strategy, because results already returned can't be retried: they fail with an
/// <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Requests marked with <see cref="NonTransactionalAttribute"/> are not run in a transaction of their own. Messages are
/// never wrapped: those published from a handler run inside its transaction. A request sent from inside another
/// request's transaction joins it: it is saved and committed with the outer request, and initializers run with
/// <see cref="TransactionInitialization.IsJoined"/> set. Whoever begins a transaction saves and commits it, so a request
/// that joins a transaction the application began itself is not saved by the behaviour, unless
/// <see cref="TransactionBehaviorOptions.ForeignTransactions"/> refuses such transactions.
/// </para>
/// <para>
/// The transaction runs through the context's execution strategy. With a retrying strategy, such as
/// <c>EnableRetryOnFailure</c>, a transient failure runs the <b>whole request again, initializers, behaviours, handler
/// and message handlers included</b>, after rolling back and clearing the change tracker. External calls, such as HTTP
/// requests or emails, are not rolled back and happen again. Make them idempotent, or perform them in a
/// <see cref="NonTransactionalAttribute"/> command sent from the host.
/// </para>
/// <para>
/// If the connection drops while committing, the commit may have succeeded on the server, and the retry saves the same
/// changes again. A unique constraint, such as on an idempotency key, turns that into an error instead of a duplicate.
/// </para>
/// <para>
/// The current transaction's state is available from <see cref="ITransactionContext"/>. Register this behaviour after
/// authentication, and before authorization and validation, to authorize inside the transaction; or after authorization
/// and validation, so that a transaction is only opened for requests that are allowed and valid.
/// </para>
/// </remarks>
public sealed class TransactionBehavior<TDbContext> : IBehavior
    where TDbContext : DbContext
{
    private static readonly ConcurrentDictionary<Type, bool> NonTransactionalRequests = new();

    private readonly TDbContext _dbContext;
    private readonly ITransactionInitializer<TDbContext>[] _initializers;
    private readonly TransactionBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionBehavior{TDbContext}"/> class, without initializers and
    /// with the default settings.
    /// </summary>
    /// <param name="dbContext">The database context to run commands in a transaction of.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public TransactionBehavior(TDbContext dbContext)
        : this(dbContext, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionBehavior{TDbContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context to run requests in a transaction of.</param>
    /// <param name="initializers">The initializers that prepare each transaction, in order.</param>
    /// <param name="options">The settings, or <see langword="null"/> for the defaults. They are copied.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dbContext"/> or <paramref name="initializers"/> is <see langword="null"/>.
    /// </exception>
    public TransactionBehavior(
        TDbContext dbContext,
        IEnumerable<ITransactionInitializer<TDbContext>> initializers,
        TransactionBehaviorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(initializers);
        _dbContext = dbContext;
        _initializers = initializers.ToArray();
        _options = (options ?? new TransactionBehaviorOptions()).Copy();
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// A transaction that the mediator didn't begin is active, and <see cref="TransactionBehaviorOptions.ForeignTransactions"/>
    /// is <see cref="ForeignTransactions.Throw"/>.
    /// </exception>
    public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default) =>
        await RunAsync<bool>(
            command,
            readOnly: false,
            async c =>
            {
                await next(c).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// A transaction that the mediator didn't begin is active, and <see cref="TransactionBehaviorOptions.ForeignTransactions"/>
    /// is <see cref="ForeignTransactions.Throw"/>.
    /// </exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default) =>
        RunAsync(command, readOnly: false, next, cancellationToken);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// A transaction that the mediator didn't begin is active, and <see cref="TransactionBehaviorOptions.ForeignTransactions"/>
    /// is <see cref="ForeignTransactions.Throw"/>.
    /// </exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default) =>
        _options.Queries == QueryTransactions.None ? next(cancellationToken) : RunAsync(query, readOnly: true, next, cancellationToken);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// The context's execution strategy retries on failure, or a transaction that the mediator didn't begin is active
    /// and <see cref="TransactionBehaviorOptions.ForeignTransactions"/> is <see cref="ForeignTransactions.Throw"/>.
    /// Thrown when the results are first enumerated.
    /// </exception>
    public IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        CancellationToken cancellationToken = default) =>
        _options.Queries == QueryTransactions.None || IsNonTransactional(query) && _dbContext.Database.CurrentTransaction is null
            ? next(cancellationToken)
            : StreamAsync(query, next, cancellationToken);

    private static bool IsNonTransactional(object request) =>
        NonTransactionalRequests.GetOrAdd(
            request.GetType(),
            static type => type.IsDefined(typeof(NonTransactionalAttribute), inherit: false));

    private ValueTask<TResult> RunAsync<TResult>(
        object request,
        bool readOnly,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return JoinAsync(request, next, cancellationToken);
        }

        return IsNonTransactional(request) ? next(cancellationToken) : RunInTransactionAsync(request, readOnly, next, cancellationToken);
    }

    // The transaction is the mediator's when the current state was set for this context instance.
    private TransactionState? OwnState()
    {
        var state = TransactionContext.State;

        if (state is not null && ReferenceEquals(state.DbContext, _dbContext))
        {
            return state;
        }

        if (_options.ForeignTransactions == ForeignTransactions.Throw)
        {
            throw new InvalidOperationException(
                $"A transaction that the mediator didn't begin is active on '{typeof(TDbContext)}'. Let the mediator " +
                "begin transactions, or set ForeignTransactions to Join.");
        }

        return null;
    }

    private async ValueTask<TResult> JoinAsync<TResult>(
        object request,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken)
    {
        // A foreign transaction is joined as it is: its owner saves and commits it.
        if (OwnState() is not { } outer)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var state = outer with { IsJoined = true };
        TransactionContext.State = state;
        await InitializeAsync(request, state, cancellationToken).ConfigureAwait(false);
        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TResult> RunInTransactionAsync<TResult>(
        object request,
        bool readOnly,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken)
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

                await using var transaction = await BeginAsync(readOnly, c).ConfigureAwait(false);
                var state = new TransactionState(_dbContext, readOnly, IsJoined: false, attempt, Guid.CreateVersion7());
                TransactionContext.State = state;
                await PrepareAsync(request, state, c).ConfigureAwait(false);

                try
                {
                    var result = await next(c).ConfigureAwait(false);

                    if (readOnly)
                    {
                        await transaction.RollbackAsync(c).ConfigureAwait(false);
                    }
                    else
                    {
                        await _dbContext.SaveChangesAsync(c).ConfigureAwait(false);
                        await transaction.CommitAsync(c).ConfigureAwait(false);
                    }

                    return result;
                }
                finally
                {
                    // Also when the query fails: disposing the transaction rolls it back, but the tracker keeps the changes.
                    if (readOnly)
                    {
                        DiscardChanges();
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<TResult> StreamAsync<TResult>(
        object query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        TransactionState? state;
        IDbContextTransaction? transaction = null;

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            state = OwnState() is { } outer ? outer with { IsJoined = true } : null;

            if (state is not null)
            {
                TransactionContext.State = state;
                await InitializeAsync(query, state, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // EF Core refuses queries in a transaction outside the strategy, and returned results can't be retried.
            if (_dbContext.Database.CreateExecutionStrategy().RetriesOnFailure)
            {
                throw new InvalidOperationException(
                    $"The stream query '{query.GetType()}' can't run in a transaction, because the execution strategy of " +
                    $"'{typeof(TDbContext)}' retries on failure. Use a query that returns a list, or a context without " +
                    "retries, or mark the stream query [NonTransactional].");
            }

            transaction = await BeginAsync(readOnly: true, cancellationToken).ConfigureAwait(false);
            state = new TransactionState(_dbContext, IsReadOnly: true, IsJoined: false, Attempt: 1, Guid.CreateVersion7());
            TransactionContext.State = state;
        }

        // Disposing an uncommitted transaction rolls it back, when the enumeration completes, fails or is stopped.
        await using (transaction)
        {
            try
            {
                if (transaction is not null)
                {
                    await PrepareAsync(query, state!, cancellationToken).ConfigureAwait(false);
                }

                await foreach (var result in WithStateAsync(state, next(cancellationToken)).ConfigureAwait(false))
                {
                    yield return result;
                }
            }
            finally
            {
                if (transaction is not null)
                {
                    DiscardChanges();
                }
            }
        }
    }

    // A foreign transaction has no state of the mediator's, so the current one, if any, is left as it is.
    private static async IAsyncEnumerable<TResult> WithStateAsync<TResult>(TransactionState? state, IAsyncEnumerable<TResult> results)
    {
        if (state is null)
        {
            await foreach (var result in results.ConfigureAwait(false))
            {
                yield return result;
            }

            yield break;
        }

        await using var enumerator = results.ConfigureAwait(false).GetAsyncEnumerator();

        while (true)
        {
            // An async iterator restores its consumer's context on every MoveNextAsync, so set the state each time.
            TransactionContext.State = state;

            if (!await enumerator.MoveNextAsync())
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    private async ValueTask<IDbContextTransaction> BeginAsync(bool readOnly, CancellationToken cancellationToken)
    {
        var isolationLevel = readOnly ? _options.QueryIsolationLevel : _options.IsolationLevel;

        return isolationLevel == IsolationLevel.Unspecified
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
    }

    // Runs the read-only statement first, then the initializers, in a transaction the behaviour began.
    private async ValueTask PrepareAsync(object request, TransactionState state, CancellationToken cancellationToken)
    {
        if (state.IsReadOnly && _options.ReadOnlyStatement is { } statement)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
        }

        await InitializeAsync(request, state, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask InitializeAsync(object request, TransactionState state, CancellationToken cancellationToken)
    {
        if (_initializers.Length == 0)
        {
            return;
        }

        var initialization = new TransactionInitialization(request, state.IsReadOnly, state.IsJoined, state.Attempt, state.AttemptId);

        foreach (var initializer in _initializers)
        {
            await initializer.InitializeAsync(_dbContext, initialization, cancellationToken).ConfigureAwait(false);
        }
    }

    // Changes a query made were rolled back, so they must not be saved later by a command in the same scope.
    private void DiscardChanges()
    {
        if (_dbContext.ChangeTracker.HasChanges())
        {
            _dbContext.ChangeTracker.Clear();
        }
    }
}
