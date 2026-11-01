using System;
using System.Collections.Generic;
using Datapoint.Mediator;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the mediator in an <see cref="IServiceCollection"/>.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMediator"/> as a scoped service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediator(this IServiceCollection services) =>
        services.AddMediator(static _ => { });

    /// <summary>
    /// Registers <see cref="IMediator"/> as a scoped service, along with the handlers and behaviours configured by
    /// <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the handlers and behaviours to register.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Also registers <see cref="IActionCatalog"/> as a singleton and <see cref="IActionDispatcher"/> as a scoped
    /// service. The catalog lists the handled commands, queries and stream queries marked with
    /// <see cref="ActionAttribute"/>, and is validated when this method runs.
    /// </para>
    /// <para>
    /// This method can be called more than once. Registering the same handler or behaviour again has no effect, and
    /// actions from every call are added to the same catalog.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A command or query would have more than one handler, or an action's name is not valid or is used twice, a message
    /// is marked as an action, or an action implements more than one action interface.
    /// </exception>
    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MediatorOptions();
        configure(options);

        // Scoped, so that handlers are resolved from the caller's scope rather than the root provider.
        services.TryAddScoped<IMediator, Mediator>();

        AddHandlers(services, options);
        AddActions(services, options);

        AddBehaviors(services, options);
        FindActionCatalogBuilder(services)!.Check(options.StartupChecks);

        return services;
    }

    private static void AddBehaviors(IServiceCollection services, MediatorOptions options)
    {
        foreach (var behavior in options.Behaviors)
        {
            var existing = FindBehavior(services, behavior.ImplementationType);

            if (existing is not null)
            {
                // Settings passed to a factory must never be silently replaced or ignored.
                if (behavior.Factory is not null || existing.ImplementationFactory is not null)
                {
                    throw new InvalidOperationException(
                        $"Cannot register the behaviour '{behavior.ImplementationType}' again, because it is already " +
                        "registered and one of the registrations has settings. Register it once.");
                }

                continue;
            }

            var lifetime = behavior.Lifetime ?? options.DefaultLifetime;
            services.Add(behavior.Factory is null
                ? ServiceDescriptor.Describe(typeof(IBehavior), behavior.ImplementationType, lifetime)
                : ServiceDescriptor.Describe(typeof(IBehavior), behavior.Factory, lifetime));
        }
    }

    private static ServiceDescriptor? FindBehavior(IServiceCollection services, Type implementationType)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IBehavior) || descriptor.IsKeyedService)
            {
                continue;
            }

            // A factory's delegate type names the type it creates, as TryAddEnumerable relies on.
            var type = descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType()
                ?? descriptor.ImplementationFactory!.GetType().GenericTypeArguments[1];

            if (type == implementationType)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static void AddActions(IServiceCollection services, MediatorOptions options)
    {
        var builder = FindActionCatalogBuilder(services);

        if (builder is null)
        {
            builder = new ActionCatalogBuilder();
            services.AddSingleton(builder);
            services.TryAddSingleton<IActionCatalog>(provider => provider.GetRequiredService<ActionCatalogBuilder>().Catalog);
            services.TryAddScoped<IActionDispatcher, ActionDispatcher>();
        }

        var actionTypes = new List<Type>();

        foreach (var handler in options.Handlers)
        {
            foreach (var handlerInterface in HandlerInterfaces.Of(handler.ImplementationType))
            {
                actionTypes.Add(handlerInterface.GetGenericArguments()[0]);
            }
        }

        builder.Add(actionTypes);
    }

    private static ActionCatalogBuilder? FindActionCatalogBuilder(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService && descriptor.ImplementationInstance is ActionCatalogBuilder builder)
            {
                return builder;
            }
        }

        return null;
    }

    private static void AddHandlers(IServiceCollection services, MediatorOptions options)
    {
        // Indexes the existing single-handler registrations, so duplicates are found without rescanning the collection.
        var singleHandlers = new Dictionary<Type, ServiceDescriptor>();

        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService && descriptor.ServiceType.IsGenericType)
            {
                singleHandlers[descriptor.ServiceType] = descriptor;
            }
        }

        foreach (var handler in options.Handlers)
        {
            var lifetime = handler.Lifetime ?? options.DefaultLifetime;

            foreach (var handlerInterface in HandlerInterfaces.Of(handler.ImplementationType))
            {
                var descriptor = ServiceDescriptor.Describe(handlerInterface, handler.ImplementationType, lifetime);

                if (HandlerInterfaces.IsMessageHandler(handlerInterface))
                {
                    services.TryAddEnumerable(descriptor);
                    continue;
                }

                if (singleHandlers.TryGetValue(handlerInterface, out var existing))
                {
                    if (existing.ImplementationType == handler.ImplementationType)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Cannot register '{handler.ImplementationType}' as '{handlerInterface}', because " +
                        $"'{existing.ImplementationType?.ToString() ?? "a factory or instance"}' is already registered. " +
                        "A command or query can only have one handler.");
                }

                services.Add(descriptor);
                singleHandlers[handlerInterface] = descriptor;
            }
        }
    }
}
