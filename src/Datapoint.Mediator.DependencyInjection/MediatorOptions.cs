using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Datapoint.Mediator;

/// <summary>
/// Configures the handlers and behaviours registered by
/// <see cref="MediatorServiceCollectionExtensions.AddMediator(IServiceCollection, Action{MediatorOptions})"/>.
/// </summary>
/// <remarks>
/// Registrations are applied in the order they are added, after the configuration callback returns.
/// </remarks>
public sealed class MediatorOptions
{
    /// <summary>
    /// Gets or sets the lifetime of handlers and behaviours that are registered without an explicit lifetime.
    /// </summary>
    /// <value>The default is <see cref="ServiceLifetime.Scoped"/>.</value>
    public ServiceLifetime DefaultLifetime { get; set; } = ServiceLifetime.Scoped;

    internal List<Registration> Handlers { get; } = [];

    internal List<Registration> Behaviors { get; } = [];

    /// <summary>
    /// Registers a handler for every command, query and message handler interface it implements.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="lifetime">The handler's lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="THandler"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler<THandler>(ServiceLifetime? lifetime = null)
        where THandler : class =>
        AddHandler(typeof(THandler), lifetime);

    /// <summary>
    /// Registers a handler for every command, query and message handler interface it implements.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="lifetime">The handler's lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handlerType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handlerType"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler(Type handlerType, ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        EnsureConcrete(handlerType, nameof(handlerType));

        if (HandlerInterfaces.Of(handlerType).Count == 0)
        {
            throw new ArgumentException(
                $"'{handlerType}' does not implement any command, query or message handler interface.",
                nameof(handlerType));
        }

        Handlers.Add(new Registration(handlerType, lifetime));
        return this;
    }

    /// <summary>
    /// Registers every command, query and message handler in an assembly. Behaviours are not registered.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The handlers' lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// Only concrete, non-generic classes are registered, ordered by their full name. Non-public classes are included.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    public MediatorOptions AddHandlersFromAssembly(Assembly assembly, ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerTypes = GetLoadableTypes(assembly)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Where(type => HandlerInterfaces.Of(type).Count > 0)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var handlerType in handlerTypes)
        {
            Handlers.Add(new Registration(handlerType, lifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers every command, query and message handler in the assembly that contains <typeparamref name="T"/>.
    /// Behaviours are not registered.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <param name="lifetime">The handlers' lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    public MediatorOptions AddHandlersFromAssemblyContaining<T>(ServiceLifetime? lifetime = null) =>
        AddHandlersFromAssembly(typeof(T).Assembly, lifetime);

    /// <summary>
    /// Registers a behaviour. Behaviours run in the order they are registered, with the first one outermost.
    /// </summary>
    /// <typeparam name="TBehavior">The behaviour type.</typeparam>
    /// <param name="lifetime">The behaviour's lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    public MediatorOptions AddBehavior<TBehavior>(ServiceLifetime? lifetime = null)
        where TBehavior : class, IBehavior =>
        AddBehavior(typeof(TBehavior), lifetime);

    /// <summary>
    /// Registers a behaviour. Behaviours run in the order they are registered, with the first one outermost.
    /// </summary>
    /// <param name="behaviorType">The behaviour type.</param>
    /// <param name="lifetime">The behaviour's lifetime, or <see langword="null"/> to use <see cref="DefaultLifetime"/>.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="behaviorType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="behaviorType"/> is not a valid behaviour type.</exception>
    public MediatorOptions AddBehavior(Type behaviorType, ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        EnsureConcrete(behaviorType, nameof(behaviorType));

        if (!typeof(IBehavior).IsAssignableFrom(behaviorType))
        {
            throw new ArgumentException($"'{behaviorType}' does not implement '{typeof(IBehavior)}'.", nameof(behaviorType));
        }

        Behaviors.Add(new Registration(behaviorType, lifetime));
        return this;
    }

    private static void EnsureConcrete(Type type, string paramName)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            throw new ArgumentException($"'{type}' must be a concrete, non-generic class.", paramName);
        }
    }

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

    internal readonly record struct Registration(Type ImplementationType, ServiceLifetime? Lifetime);
}
