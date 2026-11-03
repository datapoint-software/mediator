using System;

namespace Datapoint.Mediator;

/// <summary>
/// The exception thrown when an action's JSON arguments don't match its type.
/// </summary>
/// <remarks>
/// <para>
/// The message is for logs, and host adapters never show it to callers: they describe the failure from
/// <see cref="Field"/> instead. The message never includes the arguments, which come from the caller's input. The
/// inner exception, typically a <see cref="System.Text.Json.JsonException"/>, describes the problem for logs, and can
/// contain .NET type names and property names the caller chose.
/// </para>
/// <para>
/// Host adapters pass this exception to the application's error hook before writing their own response, so the
/// application can report it with its own error codes, from <see cref="ActionName"/> and <see cref="Field"/>.
/// </para>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidActionArgumentsException"/> class with a message, the
    /// action and the argument that failed, and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error, for logs.</param>
    /// <param name="actionName">The name of the action whose arguments failed, or <see langword="null"/>.</param>
    /// <param name="field">
    /// The argument that failed, as it's spelled on the wire, such as <c>count</c> or <c>targets[0].port</c>, or
    /// <see langword="null"/> when it isn't known. It must never be text the caller chose.
    /// </param>
    /// <param name="innerException">The exception that caused this exception, or <see langword="null"/>.</param>
    public InvalidActionArgumentsException(string message, string? actionName, string? field, Exception? innerException)
        : base(message, innerException)
    {
        ActionName = actionName;
        Field = field;
    }

    /// <summary>
    /// Gets the name of the action whose arguments failed, or <see langword="null"/> when it isn't known.
    /// </summary>
    public string? ActionName { get; }

    /// <summary>
    /// Gets the argument that failed, or <see langword="null"/> when it can't be determined.
    /// </summary>
    /// <remarks>
    /// The field is spelled as it appears on the wire, after the serializer's naming policy, such as <c>count</c> or
    /// <c>targets[0].port</c>. When Mediator throws this exception, the field is built only from names in the action's
    /// contract, never from the caller's spelling, and stops before a segment the caller chose, such as a dictionary
    /// key. It's <see langword="null"/> for malformed JSON, for missing arguments, and for an unknown property, whose
    /// name the caller chose.
    /// </remarks>
    public string? Field { get; }
}
