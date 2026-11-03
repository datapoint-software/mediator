using System;

namespace Datapoint.Mediator;

/// <summary>
/// Names a command, query or stream query, so that hosts such as an API, a CLI or an MCP server can dispatch it by
/// name through <see cref="IActionDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// A name is made of lowercase letters and digits, separated by single dots or hyphens, and is at most 64 characters
/// long, such as <c>invoices.create</c>. Underscores are not allowed, so that hosts whose tool names can't contain dots,
/// such as some MCP clients, can replace dots with underscores without two names colliding. Names must be unique.
/// Messages cannot be actions, because their handlers are not authorized.
/// </para>
/// <para>
/// The description is required, because hosts show it to their callers: as the OpenAPI summary, in CLI help and as the
/// MCP tool's description.
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
    /// <param name="description">
    /// What the action does, such as <c>Creates an invoice for a customer.</c>, shown to users and AI agents.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="description"/> is <see langword="null"/>.
    /// </exception>
    public ActionAttribute(string name, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        Name = name;
        Description = description;
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
    /// Gets a description of what the action does, shown to users and AI agents, for example as an OpenAPI summary, an
    /// MCP tool's description or in CLI help.
    /// </summary>
    /// <remarks>
    /// Every action needs one: <see cref="IActionCatalog"/> refuses an empty or whitespace description at startup.
    /// </remarks>
    public string Description { get; }
}
