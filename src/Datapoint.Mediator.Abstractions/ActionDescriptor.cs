using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Datapoint.Mediator;

/// <summary>
/// Describes an action that hosts can dispatch by name.
/// </summary>
public sealed class ActionDescriptor
{
    private readonly FrozenSet<Type> _channels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDescriptor"/> class.
    /// </summary>
    /// <param name="name">The action's name.</param>
    /// <param name="description">A description of what the action does.</param>
    /// <param name="kind">The kind of the action.</param>
    /// <param name="actionType">The type of the command, query or stream query.</param>
    /// <param name="resultType">
    /// The type of the result, or of each streamed result; <see langword="null"/> for a command without a result.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="description"/> or <paramref name="actionType"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="actionType"/> doesn't implement the interface that <paramref name="kind"/> and
    /// <paramref name="resultType"/> describe, such as <see cref="IQuery{TResult}"/> of <paramref name="resultType"/>
    /// for <see cref="ActionKind.Query"/>; or <paramref name="kind"/> isn't a defined value.
    /// </exception>
    public ActionDescriptor(string name, string description, ActionKind kind, Type actionType, Type? resultType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(actionType);
        EnsureKindMatches(kind, actionType, resultType);
        Name = name;
        Description = description;
        Kind = kind;
        ActionType = actionType;
        ResultType = resultType;
        HostProvidedProperties = FindHostProvidedProperties(actionType);
        _channels = actionType.GetCustomAttributes<ActionChannelAttribute>(inherit: false)
            .Select(channel => channel.GetType())
            .ToFrozenSet();
    }

    /// <summary>
    /// Gets the action's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a description of what the action does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the kind of the action.
    /// </summary>
    public ActionKind Kind { get; }

    /// <summary>
    /// Gets the type of the command, query or stream query.
    /// </summary>
    public Type ActionType { get; }

    /// <summary>
    /// Gets the type of the result, or of each streamed result; <see langword="null"/> for a command without a result.
    /// </summary>
    public Type? ResultType { get; }

    /// <summary>
    /// Gets the action's public properties marked with <see cref="HostProvidedAttribute"/>, on the property or on its
    /// record parameter, which hosts hide from callers.
    /// </summary>
    /// <value>The properties, or an empty list when there are none.</value>
    public IReadOnlyList<PropertyInfo> HostProvidedProperties { get; }

    /// <summary>
    /// Gets a value indicating whether the action is marked with <typeparamref name="TChannel"/>, so the host adapter
    /// that reads that attribute offers it.
    /// </summary>
    /// <typeparam name="TChannel">
    /// The adapter's attribute, such as <see cref="ApiAttribute"/>. Only that exact type matches.
    /// </typeparam>
    /// <returns><see langword="true"/> if the action is marked with <typeparamref name="TChannel"/>.</returns>
    /// <remarks>
    /// Adapters call it once per action, when they register their endpoints, tools or commands. An adapter must never
    /// offer or dispatch an action for which it returns <see langword="false"/>.
    /// </remarks>
    public bool IsOfferedBy<TChannel>()
        where TChannel : ActionChannelAttribute =>
        _channels.Contains(typeof(TChannel));

    // Checked here, so a wrong descriptor fails when it's created rather than at its first dispatch, and dispatchers can
    // rely on the kind and the result type.
    private static void EnsureKindMatches(ActionKind kind, Type actionType, Type? resultType)
    {
        var required = kind switch
        {
            ActionKind.Command when resultType is null => typeof(ICommand),
            ActionKind.CommandWithResult when resultType is not null => typeof(ICommand<>).MakeGenericType(resultType),
            ActionKind.Query when resultType is not null => typeof(IQuery<>).MakeGenericType(resultType),
            ActionKind.StreamQuery when resultType is not null => typeof(IStreamQuery<>).MakeGenericType(resultType),
            _ => null,
        };

        if (required is null || !actionType.GetInterfaces().Contains(required))
        {
            throw new ArgumentException(
                $"An action of kind '{kind}' with result type '{resultType?.ToString() ?? "none"}' must implement " +
                $"{RequiredInterfaceName(kind)}, and '{actionType}' doesn't.",
                nameof(actionType));
        }
    }

    private static string RequiredInterfaceName(ActionKind kind) =>
        kind switch
        {
            ActionKind.Command => "ICommand, without a result type",
            ActionKind.CommandWithResult => "ICommand<TResult> of its result type",
            ActionKind.Query => "IQuery<TResult> of its result type",
            ActionKind.StreamQuery => "IStreamQuery<TResult> of its result type",
            _ => "one of the interfaces of a defined ActionKind",
        };

    // Read-only, because the descriptor is shared by every host. A positional record's parameter shares its property's name. The match ignores case, as hosts do when they read a
    // property's description from its parameter.
    private static ReadOnlyCollection<PropertyInfo> FindHostProvidedProperties(Type actionType)
    {
        var parameters = actionType.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => parameter.IsDefined(typeof(HostProvidedAttribute), inherit: false))
            // Constructor parameters always have a name; only a delegate's return parameter has none.
            .Select(parameter => parameter.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0
                && (Attribute.IsDefined(property, typeof(HostProvidedAttribute), inherit: true) || parameters.Contains(property.Name)))
            .ToArray()
            .AsReadOnly();
    }
}
