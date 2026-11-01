using System;
using System.Linq;
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

    /// <summary>
    /// Registers <see cref="AuthorizationBehavior"/> as a scoped behaviour with settings, such as requiring actions to be
    /// authorized inside a transaction.
    /// </summary>
    /// <param name="options">The mediator options.</param>
    /// <param name="configure">
    /// Sets the behaviour's settings. It runs once, when this method is called; don't keep or change the settings
    /// object afterwards.
    /// </param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The behaviour runs at the position it is registered in. To authorize inside the transaction, register it after
    /// the transaction behaviour, and set <see cref="AuthorizationBehaviorOptions.RequireTransaction"/>, so that a wrong
    /// order fails instead of silently authorizing outside the transaction.
    /// </para>
    /// <para>
    /// With <see cref="AuthorizationBehaviorOptions.RequireTransaction"/> set, <c>AddMediator</c> throws
    /// <see cref="InvalidOperationException"/> when an action of a required kind, not marked with
    /// <see cref="AllowAnonymousAccessAttribute"/>, is marked with
    /// <c>Datapoint.Mediator.EntityFrameworkCore.NonTransactionalAttribute</c>, which it recognises by name. The behaviour
    /// can only be registered once with settings: registering it again makes <c>AddMediator</c> throw.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="configure"/> sets a value that is not defined.</exception>
    public static MediatorOptions AddAuthorizationBehavior(
        this MediatorOptions options,
        Action<AuthorizationBehaviorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new AuthorizationBehaviorOptions();
        configure(settings);

        if (settings.RequireTransaction != TransactionRequirement.None)
        {
            var requirement = settings.RequireTransaction;
            options.AddStartupCheck(context => RefuseNonTransactionalActions(context, requirement));
        }

        return options.AddBehavior(provider => new AuthorizationBehavior(provider, settings), ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers <see cref="AuthenticationBehavior"/> as a scoped behaviour, so that commands, queries and stream queries
    /// are refused when there is no signed-in user, before the behaviours registered after it run.
    /// </summary>
    /// <param name="options">The mediator options.</param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// Register it first, before the transaction behaviour, so callers who aren't signed in never begin a transaction.
    /// Calling this method more than once has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static MediatorOptions AddAuthenticationBehavior(this MediatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior<AuthenticationBehavior>(ServiceLifetime.Scoped);
    }

    // Matched by name, as adapters match the authorization exceptions, so this package doesn't depend on EF Core.
    private const string NonTransactionalAttributeName = "Datapoint.Mediator.EntityFrameworkCore.NonTransactionalAttribute";

    private static void RefuseNonTransactionalActions(MediatorStartupContext context, TransactionRequirement requirement)
    {
        var refused = context.RequestTypes
            .Where(type => requirement == TransactionRequirement.CommandsAndQueries || IsCommand(type))
            .Where(type => !type.IsDefined(typeof(AllowAnonymousAccessAttribute), inherit: false))
            .Where(type => type.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == NonTransactionalAttributeName))
            .Select(type => $"'{type}'")
            .ToArray();

        if (refused.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", refused)} must be authorized inside a transaction, but are marked [NonTransactional]. " +
                "Remove the attribute, mark them [AllowAnonymousAccess] if they need no authorization, or lower " +
                "RequireTransaction.");
        }
    }

    private static bool IsCommand(Type type) =>
        typeof(ICommand).IsAssignableFrom(type)
        || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
}
