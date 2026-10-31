using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The exception thrown when the signed-in user may not run a command, query or stream query.
/// </summary>
/// <remarks>
/// An API typically maps this exception to an HTTP 403 response.
/// </remarks>
public sealed class AccessDeniedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessDeniedException"/> class.
    /// </summary>
    public AccessDeniedException()
        : base("Access is denied.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessDeniedException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessDeniedException"/> class with a message and the
    /// exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
