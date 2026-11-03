using System;
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
    public ActionDescriptor(string name, string description, ActionKind kind, Type actionType, Type? resultType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(actionType);
        Name = name;
        Description = description;
        Kind = kind;
        ActionType = actionType;
        ResultType = resultType;
        HostProvidedProperties = FindHostProvidedProperties(actionType);
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

    // Read-only, because the descriptor is shared by every host. A positional record's parameter shares its property's name. The match ignores case, as hosts do when they read a
    // property's description from its parameter.
    private static ReadOnlyCollection<PropertyInfo> FindHostProvidedProperties(Type actionType)
    {
        var parameters = actionType.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => parameter.IsDefined(typeof(HostProvidedAttribute), inherit: false))
            .Select(parameter => parameter.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0
                && (Attribute.IsDefined(property, typeof(HostProvidedAttribute), inherit: true) || parameters.Contains(property.Name)))
            .ToArray()
            .AsReadOnly();
    }
}
