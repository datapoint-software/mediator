using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datapoint.Mediator.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="IAuthorizer{TAction}"/> implementations in an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class AuthorizerServiceCollectionExtensions
{
    /// <summary>
    /// Registers every <see cref="IAuthorizer{TAction}"/> implementation in an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The authorizers' lifetime. The default is <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Only concrete, non-generic classes are registered, ordered by their full name. Non-public classes are included.
    /// Registering the same authorizer again has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assembly"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddAuthorizersFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var authorizerTypes = GetLoadableTypes(assembly)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var authorizerType in authorizerTypes)
        {
            foreach (var @interface in authorizerType.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IAuthorizer<>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Describe(@interface, authorizerType, lifetime));
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Registers every <see cref="IAuthorizer{TAction}"/> implementation in the assembly that contains
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The authorizers' lifetime. The default is <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAuthorizersFromAssemblyContaining<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddAuthorizersFromAssembly(typeof(T).Assembly, lifetime);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
