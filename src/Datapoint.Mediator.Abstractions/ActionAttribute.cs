using System;

namespace Datapoint.Mediator;

/// <summary>
/// Names a command, query or stream query, so that hosts such as an API, a CLI or an MCP server can dispatch it by
/// name through <see cref="IActionDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// A name is made of lowercase letters and digits, separated by single dots, hyphens or underscores, and is at most
/// 64 characters long, such as <c>invoice.create</c>. Names must be unique. Messages cannot be actions, because their
/// handlers are not authorized.
/// </para>
/// <para>
/// The attribute is not inherited, so a derived type must be named on its own.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionAttribute"/> class.
    /// </summary>
    /// <param name="name">The action's name, such as <c>invoice.create</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public ActionAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Gets the action's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the action is hidden from hosts: not listed by <see cref="IActionCatalog"/>, and
    /// not dispatchable by name.
    /// </summary>
    /// <value>The default is <see langword="false"/>.</value>
    /// <remarks>
    /// A hidden action keeps its name, which must still be unique, and can still be sent through
    /// <see cref="IMediator"/>.
    /// </remarks>
    public bool Hidden { get; init; }

    /// <summary>
    /// Gets a description of what the action does, shown to users and AI agents, for example as an MCP tool's
    /// description or in CLI help.
    /// </summary>
    public string? Description { get; init; }
}
