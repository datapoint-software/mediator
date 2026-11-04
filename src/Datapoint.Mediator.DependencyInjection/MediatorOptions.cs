using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    internal List<Action<MediatorStartupContext>> StartupChecks { get; } = [];

    /// <summary>
    /// Registers a handler, with <see cref="DefaultLifetime"/>, for every command, query and message handler interface it
    /// implements.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="THandler"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler<THandler>()
        where THandler : class =>
        AddHandlerCore(typeof(THandler), null);

    /// <summary>
    /// Registers a handler for every command, query and message handler interface it implements.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="lifetime">The handler's lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="THandler"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler<THandler>(ServiceLifetime lifetime)
        where THandler : class =>
        AddHandlerCore(typeof(THandler), lifetime);

    /// <summary>
    /// Registers a handler, with <see cref="DefaultLifetime"/>, for every command, query and message handler interface it
    /// implements.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handlerType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handlerType"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler(Type handlerType) =>
        AddHandlerCore(handlerType, null);

    /// <summary>
    /// Registers a handler for every command, query and message handler interface it implements.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="lifetime">The handler's lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handlerType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handlerType"/> is not a valid handler type.</exception>
    public MediatorOptions AddHandler(Type handlerType, ServiceLifetime lifetime) =>
        AddHandlerCore(handlerType, lifetime);

    /// <summary>
    /// Registers every command, query and message handler in an assembly, with <see cref="DefaultLifetime"/>. Behaviours
    /// are not registered.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// Only concrete, non-generic classes are registered, ordered by their full name. Non-public classes are included.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    public MediatorOptions AddHandlersFromAssembly(Assembly assembly) =>
        AddHandlersFromAssemblyCore(assembly, null);

    /// <summary>
    /// Registers every command, query and message handler in an assembly. Behaviours are not registered.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The handlers' lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// Only concrete, non-generic classes are registered, ordered by their full name. Non-public classes are included.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    public MediatorOptions AddHandlersFromAssembly(Assembly assembly, ServiceLifetime lifetime) =>
        AddHandlersFromAssemblyCore(assembly, lifetime);

    /// <summary>
    /// Registers every command, query and message handler in the assembly that contains <typeparamref name="T"/>, with
    /// <see cref="DefaultLifetime"/>. Behaviours are not registered.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <returns>These options, for chaining.</returns>
    public MediatorOptions AddHandlersFromAssemblyContaining<T>() =>
        AddHandlersFromAssemblyCore(typeof(T).Assembly, null);

    /// <summary>
    /// Registers every command, query and message handler in the assembly that contains <typeparamref name="T"/>.
    /// Behaviours are not registered.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <param name="lifetime">The handlers' lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    public MediatorOptions AddHandlersFromAssemblyContaining<T>(ServiceLifetime lifetime) =>
        AddHandlersFromAssemblyCore(typeof(T).Assembly, lifetime);

    /// <summary>
    /// Registers a behaviour, with <see cref="DefaultLifetime"/>. Behaviours run in the order they are registered, with
    /// the first one outermost.
    /// </summary>
    /// <typeparam name="TBehavior">The behaviour type.</typeparam>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TBehavior"/> is not a concrete class.</exception>
    public MediatorOptions AddBehavior<TBehavior>()
        where TBehavior : class, IBehavior =>
        AddBehaviorCore(typeof(TBehavior), null);

    /// <summary>
    /// Registers a behaviour. Behaviours run in the order they are registered, with the first one outermost.
    /// </summary>
    /// <typeparam name="TBehavior">The behaviour type.</typeparam>
    /// <param name="lifetime">The behaviour's lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TBehavior"/> is not a concrete class.</exception>
    public MediatorOptions AddBehavior<TBehavior>(ServiceLifetime lifetime)
        where TBehavior : class, IBehavior =>
        AddBehaviorCore(typeof(TBehavior), lifetime);

    /// <summary>
    /// Registers a behaviour, with <see cref="DefaultLifetime"/>. Behaviours run in the order they are registered, with
    /// the first one outermost.
    /// </summary>
    /// <param name="behaviorType">The behaviour type.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="behaviorType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="behaviorType"/> is not a valid behaviour type.</exception>
    public MediatorOptions AddBehavior(Type behaviorType) =>
        AddBehaviorCore(behaviorType, null);

    /// <summary>
    /// Registers a behaviour. Behaviours run in the order they are registered, with the first one outermost.
    /// </summary>
    /// <param name="behaviorType">The behaviour type.</param>
    /// <param name="lifetime">The behaviour's lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="behaviorType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="behaviorType"/> is not a valid behaviour type.</exception>
    public MediatorOptions AddBehavior(Type behaviorType, ServiceLifetime lifetime) =>
        AddBehaviorCore(behaviorType, lifetime);

    /// <summary>
    /// Registers a behaviour created by a factory, with <see cref="DefaultLifetime"/>, for behaviours that need settings
    /// in their constructor. Behaviours run in the order they are registered, with the first one outermost.
    /// </summary>
    /// <typeparam name="TBehavior">The behaviour type, which must be a concrete class.</typeparam>
    /// <param name="factory">Creates the behaviour from the service provider of the current scope.</param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// A behaviour type created by a factory can only be registered once: registering the same type again, with or
    /// without a factory, makes <c>AddMediator</c> throw <see cref="InvalidOperationException"/>, so settings are never
    /// silently ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TBehavior"/> is not a concrete class.</exception>
    public MediatorOptions AddBehavior<TBehavior>(Func<IServiceProvider, TBehavior> factory)
        where TBehavior : class, IBehavior =>
        AddBehaviorCore(factory, null);

    /// <summary>
    /// Registers a behaviour created by a factory, for behaviours that need settings in their constructor. Behaviours run
    /// in the order they are registered, with the first one outermost.
    /// </summary>
    /// <typeparam name="TBehavior">The behaviour type, which must be a concrete class.</typeparam>
    /// <param name="factory">Creates the behaviour from the service provider of the current scope.</param>
    /// <param name="lifetime">The behaviour's lifetime.</param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// A behaviour type created by a factory can only be registered once: registering the same type again, with or
    /// without a factory, makes <c>AddMediator</c> throw <see cref="InvalidOperationException"/>, so settings are never
    /// silently ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TBehavior"/> is not a concrete class.</exception>
    public MediatorOptions AddBehavior<TBehavior>(Func<IServiceProvider, TBehavior> factory, ServiceLifetime lifetime)
        where TBehavior : class, IBehavior =>
        AddBehaviorCore(factory, lifetime);

    /// <summary>
    /// Registers a check that runs when <c>AddMediator</c> is called, so a configuration mistake fails at startup
    /// rather than at the first request.
    /// </summary>
    /// <param name="check">
    /// Inspects the registered commands, queries and stream queries, and throws <see cref="InvalidOperationException"/>
    /// to report a mistake.
    /// </param>
    /// <returns>These options, for chaining.</returns>
    /// <remarks>
    /// Checks run at the end of every <c>AddMediator</c> call, including later ones, over every request type registered
    /// so far, so a request added by a later call is checked too.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="check"/> is <see langword="null"/>.</exception>
    public MediatorOptions AddStartupCheck(Action<MediatorStartupContext> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        StartupChecks.Add(check);
        return this;
    }

    // A null lifetime means DefaultLifetime, read when AddMediator applies the registrations.
    private MediatorOptions AddHandlerCore(Type handlerType, ServiceLifetime? lifetime)
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

    private MediatorOptions AddHandlersFromAssemblyCore(Assembly assembly, ServiceLifetime? lifetime)
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

    private MediatorOptions AddBehaviorCore(Type behaviorType, ServiceLifetime? lifetime)
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

    private MediatorOptions AddBehaviorCore<TBehavior>(Func<IServiceProvider, TBehavior> factory, ServiceLifetime? lifetime)
        where TBehavior : class, IBehavior
    {
        ArgumentNullException.ThrowIfNull(factory);
        EnsureConcrete(typeof(TBehavior), nameof(TBehavior));

        Behaviors.Add(new Registration(typeof(TBehavior), lifetime, factory));
        return this;
    }

    private static void EnsureConcrete(Type type, string paramName)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            throw new ArgumentException($"'{type}' must be a concrete, non-generic class.", paramName);
        }
    }

    // Excluded from coverage: ReflectionTypeLoadException only occurs when an assembly references a dependency that is
    // missing at runtime, which a test can only reproduce with a deliberately broken build.
    [ExcludeFromCodeCoverage(Justification = "Requires an assembly with a missing dependency.")]
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

    // Factory is set for behaviours created by a factory; its delegate type names the implementation type.
    internal readonly record struct Registration(
        Type ImplementationType,
        ServiceLifetime? Lifetime,
        Func<IServiceProvider, object>? Factory = null);
}
