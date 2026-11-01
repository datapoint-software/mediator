using System;
using Datapoint.Mediator.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="IAuthorizationObserver"/> implementations in an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class AuthorizationObserverServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IAuthorizationObserver"/>, which is told every authorization outcome, allowed or not.
    /// </summary>
    /// <typeparam name="TObserver">The type of the observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The observer's lifetime. The default is <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Observers run in registration order. If one throws, the action does not run. Registering the same observer
    /// again has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAuthorizationObserver<TObserver>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TObserver : class, IAuthorizationObserver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IAuthorizationObserver), typeof(TObserver), lifetime));
        return services;
    }
}
