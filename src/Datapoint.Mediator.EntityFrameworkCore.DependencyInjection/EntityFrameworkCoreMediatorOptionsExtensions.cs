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
    /// <para>
    /// The behaviour runs at the position it is registered in, relative to other behaviours. Register it after
    /// authorization and validation, so that a transaction is only opened for commands that are allowed and valid; or
    /// before them, after authentication, to authorize and validate inside the transaction. The behaviour is always
    /// scoped, so it uses the same context as the handlers, and runs every registered
    /// <see cref="ITransactionInitializer{TDbContext}"/>.
    /// </para>
    /// <para>
    /// Calling this method more than once has no effect, but calling it together with the overload that takes settings
    /// makes <c>AddMediator</c> throw <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static MediatorOptions AddTransactionBehavior<TDbContext>(this MediatorOptions options)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior<TransactionBehavior<TDbContext>>(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers <see cref="TransactionBehavior{TDbContext}"/> as a scoped behaviour with settings, such as isolation
    /// levels or read-only transactions for queries.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context, which must also be registered.</typeparam>
    /// <param name="options">The mediator options.</param>
    /// <param name="configure">
    /// Sets the behaviour's settings. It runs once, when this method is called; don't keep or change the settings
    /// object afterwards.
    /// </param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// The behaviour runs at the position it is registered in, relative to other behaviours. It can only be registered
    /// once: registering it again makes <c>AddMediator</c> throw <see cref="InvalidOperationException"/>, so settings
    /// are never silently ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="configure"/> sets a value that is not defined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="configure"/> sets an empty <see cref="TransactionBehaviorOptions.ReadOnlyStatement"/>.
    /// </exception>
    public static MediatorOptions AddTransactionBehavior<TDbContext>(
        this MediatorOptions options,
        Action<TransactionBehaviorOptions> configure)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new TransactionBehaviorOptions();
        configure(settings);

        return options.AddBehavior(
            provider => new TransactionBehavior<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetServices<ITransactionInitializer<TDbContext>>(),
                settings),
            ServiceLifetime.Scoped);
    }
}
