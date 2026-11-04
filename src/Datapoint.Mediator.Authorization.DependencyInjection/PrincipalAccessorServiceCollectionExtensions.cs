using System;
using System.Security.Claims;
using Datapoint.Mediator.Authorization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering an <see cref="IPrincipalAccessor"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PrincipalAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="FixedPrincipalAccessor"/> that provides <paramref name="principal"/> for every command and
    /// query.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="principal">The user, or <see langword="null"/> when no one is signed in.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Use it in hosts that act for a single user, such as a CLI or an MCP server over stdio. Registering the same
    /// principal again has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Another <see cref="IPrincipalAccessor"/> is already registered. An application has one way of finding the current
    /// user, so a second one is a configuration mistake rather than something to override silently.
    /// </exception>
    public static IServiceCollection AddFixedPrincipalAccessor(this IServiceCollection services, ClaimsPrincipal? principal)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (PrincipalAccessorRegistration.IsRegistered(
            services,
            descriptor => descriptor.ImplementationInstance is FixedPrincipalAccessor existing && existing.Principal == principal,
            $"a {nameof(FixedPrincipalAccessor)}"))
        {
            return services;
        }

        services.AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal));
        return services;
    }
}
