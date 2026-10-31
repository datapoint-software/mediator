using System;
using System.Text.Json;

namespace Datapoint.Mediator.ModelContextProtocol;

/// <summary>
/// Configures how actions are offered as MCP tools.
/// </summary>
public sealed class MediatorMcpOptions
{
    /// <summary>
    /// Gets or sets which actions to offer as tools, or <see langword="null"/> to offer every action in the catalog.
    /// </summary>
    public Func<ActionDescriptor, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the most results a stream query returns before its results are cut short.
    /// </summary>
    /// <value>The default is 100. It must be at least 1.</value>
    public int MaxStreamResults { get; set; } = 100;

    /// <summary>
    /// Gets or sets the options used for tool arguments, input schemas and results, or <see langword="null"/> to use
    /// <see cref="JsonSerializerOptions.Web"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into a message for the AI agent, or returns
    /// <see langword="null"/> to report a generic error that reveals nothing about it.
    /// </summary>
    /// <remarks>
    /// The message is sent to the agent, so it must never contain secrets, personal data or internal details. For
    /// example, to report FluentValidation failures, include only each failure's property name and error message, and
    /// never its attempted value.
    /// </remarks>
    public Func<Exception, string?>? ErrorMessage { get; set; }
}
