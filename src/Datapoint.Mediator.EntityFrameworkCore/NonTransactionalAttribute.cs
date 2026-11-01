using System;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Marks a command, query or stream query that <see cref="TransactionBehavior{TDbContext}"/> does not run in a
/// transaction of its own.
/// </summary>
/// <remarks>
/// Use it for commands whose handlers have side effects that cannot be undone or repeated, such as sending an email, or
/// that manage their own transactions. Their handlers must call <c>SaveChangesAsync</c> themselves. For queries and
/// stream queries, it only matters when <see cref="TransactionBehaviorOptions.Queries"/> runs them in transactions. The
/// attribute has no effect when the request is sent from inside another request's transaction, which it joins. The
/// attribute is not inherited, so a type derived from a non-transactional request runs in a transaction unless it is
/// marked too.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NonTransactionalAttribute : Attribute;
