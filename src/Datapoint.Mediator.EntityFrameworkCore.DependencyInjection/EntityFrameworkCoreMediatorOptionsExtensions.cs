using System;
using Datapoint.Mediator;
using Datapoint.Mediator.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Entity Framework Core transactions in <see cref="MediatorOptions"/>.
/// </summary>
public static class EntityFrameworkCoreMediatorOptionsExtensions
{
    /// <summary>
    /// Registers <see cref="TransactionBehavior{TDbContext}"/> as a scoped behaviour, so that each command runs in a
    /// <typeparamref name="TDbContext"/> transaction and its changes are saved once, after the handler succeeds.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context, which must also be registered.</typeparam>
    /// <param name="options">The mediator options.</param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// The behaviour runs at the position it is registered in, relative to other behaviours. Register it last, after
    /// authorization and validation, so that a transaction is only opened for commands that are allowed and valid. The
    /// behaviour is always scoped, so it uses the same context as the handlers. Calling this method more than once has
    /// no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static MediatorOptions AddTransactionBehavior<TDbContext>(this MediatorOptions options)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior<TransactionBehavior<TDbContext>>(ServiceLifetime.Scoped);
    }
}
