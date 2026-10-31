using System;

namespace Datapoint.Mediator;

/// <summary>
/// The exception thrown when an action's JSON arguments don't match its type.
/// </summary>
/// <remarks>
/// The message never includes the arguments, which come from the caller's input. The inner exception, typically a
/// <see cref="System.Text.Json.JsonException"/>, describes the problem for logs.
/// </remarks>
public sealed class InvalidActionArgumentsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidActionArgumentsException"/> class.
    /// </summary>
    public InvalidActionArgumentsException()
        : base("The action's arguments are not valid.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidActionArgumentsException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidActionArgumentsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidActionArgumentsException"/> class with a message and the
    /// exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public InvalidActionArgumentsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
