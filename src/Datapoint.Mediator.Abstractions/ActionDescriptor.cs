using System;

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
    /// <param name="description">A description of what the action does, or <see langword="null"/>.</param>
    /// <param name="kind">The kind of the action.</param>
    /// <param name="actionType">The type of the command, query or stream query.</param>
    /// <param name="resultType">
    /// The type of the result, or of each streamed result; <see langword="null"/> for a command without a result.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="actionType"/> is <see langword="null"/>.
    /// </exception>
    public ActionDescriptor(string name, string? description, ActionKind kind, Type actionType, Type? resultType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actionType);
        Name = name;
        Description = description;
        Kind = kind;
        ActionType = actionType;
        ResultType = resultType;
    }

    /// <summary>
    /// Gets the action's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a description of what the action does, or <see langword="null"/>.
    /// </summary>
    public string? Description { get; }

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
}
