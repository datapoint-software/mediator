using System;
using System.Text.Json;

namespace Datapoint.Mediator.CommandLine;

/// <summary>
/// Configures how actions are added as CLI commands.
/// </summary>
public sealed class MediatorCommandLineOptions
{
    /// <summary>
    /// Gets or sets which actions to add, or <see langword="null"/> to add every action in the catalog.
    /// </summary>
    public Func<ActionDescriptor, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the options used for arguments and output, or <see langword="null"/> to use
    /// <see cref="JsonSerializerOptions.Web"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into a message for the user, or returns
    /// <see langword="null"/> to report an unexpected error without details. A message from this function exits with
    /// <see cref="ActionExitCodes.InvalidArguments"/>.
    /// </summary>
    /// <remarks>
    /// For example, to report FluentValidation failures, include each failure's property name and error message.
    /// </remarks>
    public Func<Exception, string?>? ErrorMessage { get; set; }
}
