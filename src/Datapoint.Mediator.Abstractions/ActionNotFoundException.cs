using System;

namespace Datapoint.Mediator;

/// <summary>
/// The exception thrown when the action catalog has no action with the requested name.
/// </summary>
/// <remarks>
/// Host adapters report an action they don't offer exactly like an unknown one, so callers can't learn which other
/// actions exist. The message doesn't include the requested name, which comes from the caller's input.
/// </remarks>
public sealed class ActionNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionNotFoundException"/> class.
    /// </summary>
    public ActionNotFoundException()
        : base("The requested action is not available.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionNotFoundException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ActionNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionNotFoundException"/> class with a message and the exception
    /// that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ActionNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
