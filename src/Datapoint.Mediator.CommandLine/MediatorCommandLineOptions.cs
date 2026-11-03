using System;
using System.Text.Json;

namespace Datapoint.Mediator.CommandLine;

/// <summary>
/// Configures how actions are added as CLI commands.
/// </summary>
public sealed class MediatorCommandLineOptions
{
    /// <summary>
    /// Gets or sets which actions to add, or <see langword="null"/> to add every action marked with
    /// <see cref="CommandLineAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The filter only sees actions marked with <see cref="CommandLineAttribute"/>, so it can narrow them but never add
    /// others.
    /// </remarks>
    public Func<ActionDescriptor, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the options used for arguments and output, or <see langword="null"/> to use
    /// <see cref="JsonSerializerOptions.Web"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into a message for the user, or returns
    /// <see langword="null"/> to keep Mediator's message for invalid arguments and authorization failures, or to report
    /// an unexpected error without details for any other exception. A message for an exception Mediator doesn't know
    /// exits with <see cref="ActionExitCodes.InvalidArguments"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Write your own text: an exception's message is for logs, including your own exceptions'. For example, to report
    /// FluentValidation failures, include each failure's property name and error message.
    /// </para>
    /// <para>
    /// It also receives <see cref="InvalidActionArgumentsException"/>, before Mediator writes its own message, so you can
    /// report it your own way, from its <c>ActionName</c>, <c>Field</c> and <c>Reason</c>. Never return its message or its inner
    /// exception's message, which can contain .NET type names and property names the caller chose.
    /// </para>
    /// <para>
    /// It also receives <c>AuthenticationRequiredException</c> and <c>AccessDeniedException</c>, so you can word them
    /// your own way. The exit code stays <see cref="ActionExitCodes.AuthenticationRequired"/> or
    /// <see cref="ActionExitCodes.AccessDenied"/>, whatever the function returns. Don't reveal why access was denied,
    /// such as a role or who owns a resource.
    /// </para>
    /// <para>
    /// The messages Mediator writes itself never contain argument values, .NET type names, stack traces or paths. They
    /// are meant for developers and agents, not for display in a user interface.
    /// </para>
    /// </remarks>
    public Func<Exception, string?>? ErrorMessage { get; set; }
}
