using System;
using Datapoint.Mediator.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="IAuthorizationContext"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class AuthorizationContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuthorizationContext"/> as the singleton <see cref="IAuthorizationContext"/>, so that
    /// handlers and behaviours can read the decision that allowed the current action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The context reads the decision from the current asynchronous flow, so one instance serves every scope. Calling
    /// this method more than once has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAuthorizationContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAuthorizationContext, AuthorizationContext>();
        return services;
    }
}
