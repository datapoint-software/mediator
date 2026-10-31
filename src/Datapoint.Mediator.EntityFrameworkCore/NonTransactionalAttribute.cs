using System;

namespace Datapoint.Mediator.EntityFrameworkCore;

/// <summary>
/// Marks a command that <see cref="TransactionBehavior{TDbContext}"/> does not run in a transaction.
/// </summary>
/// <remarks>
/// Use it for commands whose handlers have side effects that cannot be undone or repeated, such as sending an email, or
/// that manage their own transactions. Their handlers must call <c>SaveChangesAsync</c> themselves. The attribute is
/// not inherited, so a type derived from a non-transactional command runs in a transaction unless it is marked too.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NonTransactionalAttribute : Attribute;
