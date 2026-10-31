using System;
using Datapoint.Mediator;
using Datapoint.Mediator.Authorization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering authorization in <see cref="MediatorOptions"/>.
/// </summary>
public static class AuthorizationMediatorOptionsExtensions
{
    /// <summary>
    /// Registers <see cref="AuthorizationBehavior"/> as a scoped behaviour, so that commands, queries and stream queries
    /// are authorized before their handler runs.
    /// </summary>
    /// <param name="options">The mediator options.</param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The behaviour runs at the position it is registered in, relative to other behaviours. Register it before any
    /// validation behaviour, and after any behaviour that should handle authorization failures.
    /// </para>
    /// <para>
    /// The host must also register an <see cref="IPrincipalAccessor"/>, and authorizers must be registered, for example
    /// with <see cref="AuthorizerServiceCollectionExtensions.AddAuthorizersFromAssembly"/>. The behaviour is always
    /// scoped, whatever <see cref="MediatorOptions.DefaultLifetime"/> is. Calling this method more than once has no
    /// effect.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static MediatorOptions AddAuthorizationBehavior(this MediatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior<AuthorizationBehavior>(ServiceLifetime.Scoped);
    }
}
