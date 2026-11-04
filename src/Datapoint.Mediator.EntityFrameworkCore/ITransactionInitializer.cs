using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Prepares each transaction that <see cref="TransactionBehavior{TDbContext}"/> begins, before anything else runs in it,
/// for example to apply a transaction-local setting for PostgreSQL row-level security.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <remarks>
/// <para>
/// Initializers run in registration order, right after the transaction begins and after
/// <see cref="TransactionBehaviorOptions.ReadOnlyStatement"/>, before the rest of the pipeline, such as authorization,
/// validation and the handler. They run again on every retry. They also run when a request joins the transaction of an
/// outer request, with <see cref="TransactionInitialization.IsJoined"/> set, so an initializer can check that the
/// nested request matches; an initializer with nothing to check returns early.
/// </para>
/// <para>
/// If an initializer throws, the request fails and the transaction is rolled back. Initializers run before
/// authorization, so take values such as a tenant from the authenticated user, never from the request alone: a tenant
/// taken from the request must be checked against the user's memberships first.
/// </para>
/// </remarks>
public interface ITransactionInitializer<in TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Prepares the transaction.
    /// </summary>
    /// <param name="dbContext">The database context, in its transaction.</param>
    /// <param name="transaction">The request and the transaction being prepared.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the transaction is prepared. Await it only once.</returns>
    ValueTask InitializeAsync(TDbContext dbContext, TransactionInitialization transaction, CancellationToken cancellationToken = default);
}
