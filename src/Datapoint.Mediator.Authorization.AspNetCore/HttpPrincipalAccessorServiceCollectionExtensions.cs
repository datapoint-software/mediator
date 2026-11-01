using System;
using Datapoint.Mediator.Authorization;
using Datapoint.Mediator.Authorization.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="HttpPrincipalAccessor"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class HttpPrincipalAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpPrincipalAccessor"/> as the scoped <see cref="IPrincipalAccessor"/>, so that commands and
    /// queries run on behalf of the current request's user. Also registers <c>IHttpContextAccessor</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>Calling this method again has no effect.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Another <see cref="IPrincipalAccessor"/> is already registered. An application has one way of finding the current
    /// user, so a second one is a configuration mistake rather than something to override silently.
    /// </exception>
    public static IServiceCollection AddHttpPrincipalAccessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IPrincipalAccessor) || descriptor.IsKeyedService)
            {
                continue;
            }

            if (descriptor.ImplementationType == typeof(HttpPrincipalAccessor))
            {
                return services;
            }

            throw new InvalidOperationException(
                $"Cannot register the {nameof(HttpPrincipalAccessor)}, because another {nameof(IPrincipalAccessor)} is " +
                "already registered. Register only one.");
        }

        services.AddHttpContextAccessor();
        services.AddScoped<IPrincipalAccessor, HttpPrincipalAccessor>();
        return services;
    }
}
