using System;
using System.Text.Json;

namespace Datapoint.Mediator.ModelContextProtocol;

/// <summary>
/// Configures how actions are offered as MCP tools.
/// </summary>
public sealed class MediatorMcpOptions
{
    /// <summary>
    /// Gets or sets which actions to offer as tools, or <see langword="null"/> to offer every action marked with
    /// <see cref="ModelContextProtocolAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The filter only sees actions marked with <see cref="ModelContextProtocolAttribute"/>, so it can narrow them but
    /// never add others.
    /// </remarks>
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
    /// Gets or sets a function that turns an exception thrown by an action into a text message for the AI agent, or returns
    /// <see langword="null"/> to keep Mediator's message for invalid arguments and authorization failures, or to report a
    /// generic error that reveals nothing about any other exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The message is sent to the agent, so it must never contain secrets, personal data or internal details. Write your
    /// own text: an exception's message is for logs, including your own exceptions'. For example, to report
    /// FluentValidation failures, include only each failure's property name and error message, and never its attempted
    /// value.
    /// </para>
    /// <para>
    /// It also receives <see cref="InvalidActionArgumentsException"/>, before Mediator writes its own message, so you can
    /// report it your own way, from its <c>ActionName</c>, <c>Field</c> and <c>Reason</c>. Never return its message or its inner
    /// exception's message, which can contain .NET type names and property names the caller chose.
    /// </para>
    /// <para>
    /// It also receives <c>AuthenticationRequiredException</c> and <c>AccessDeniedException</c>, so you can word them
    /// your own way. The result is still an error, whatever the function returns. Don't reveal why access was denied,
    /// such as a role or who owns a resource.
    /// </para>
    /// <para>
    /// The messages Mediator writes itself never contain argument values, .NET type names, stack traces or paths. They
    /// are meant for developers and agents, not for display in a user interface.
    /// </para>
    /// </remarks>
    public Func<Exception, string?>? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into structured content for the AI agent, or
    /// returns <see langword="null"/> to keep Mediator's message, as for <see cref="ErrorMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is serialized with <see cref="SerializerOptions"/> and must serialize to a JSON object, such as an
    /// anonymous object with an error code and field errors. It's returned as the error result's structured content,
    /// and its JSON as the result's text, so agents can read it either way. Any other value is reported as a generic
    /// error.
    /// </para>
    /// <para>
    /// It receives the same exceptions as <see cref="ErrorMessage"/>, follows the same rules, and can't be set together
    /// with it. The content is sent to the agent, so it must never contain secrets, personal data, internal details or
    /// attempted values.
    /// </para>
    /// </remarks>
    public Func<Exception, object?>? ErrorContent { get; set; }
}
