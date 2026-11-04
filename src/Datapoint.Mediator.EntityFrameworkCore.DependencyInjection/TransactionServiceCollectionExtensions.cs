using System;
using Datapoint.Mediator;
using Datapoint.Mediator.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering transaction services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class TransactionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TransactionContext"/> as the singleton <see cref="ITransactionContext"/>, so that behaviours,
    /// handlers and observers can read the state of the current request's transaction.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The context reads the state from the current asynchronous flow, so one instance serves every scope. Calling this
    /// method more than once has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddTransactionContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITransactionContext, TransactionContext>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="ITransactionInitializer{TDbContext}"/> as a scoped service. It prepares each transaction
    /// of <typeparamref name="TDbContext"/> before anything else runs in it.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context.</typeparam>
    /// <typeparam name="TInitializer">The type of the initializer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Initializers run in registration order. Registering the same initializer again has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddTransactionInitializer<TDbContext, TInitializer>(this IServiceCollection services)
        where TDbContext : DbContext
        where TInitializer : class, ITransactionInitializer<TDbContext> =>
        services.AddTransactionInitializer<TDbContext, TInitializer>(ServiceLifetime.Scoped);

    /// <summary>
    /// Registers an <see cref="ITransactionInitializer{TDbContext}"/>, which prepares each transaction of
    /// <typeparamref name="TDbContext"/> before anything else runs in it.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context.</typeparam>
    /// <typeparam name="TInitializer">The type of the initializer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The initializer's lifetime.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Initializers run in registration order. Registering the same initializer again has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddTransactionInitializer<TDbContext, TInitializer>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TDbContext : DbContext
        where TInitializer : class, ITransactionInitializer<TDbContext>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(ITransactionInitializer<TDbContext>), typeof(TInitializer), lifetime));
        return services;
    }
}
