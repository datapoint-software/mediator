using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// The exception thrown when a command, query or stream query requires a signed-in user and there is none.
/// </summary>
/// <remarks>
/// An API typically maps this exception to an HTTP 401 response.
/// </remarks>
public sealed class AuthenticationRequiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class.
    /// </summary>
    public AuthenticationRequiredException()
        : base("Authentication is required.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AuthenticationRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with a message and the
    /// exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public AuthenticationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
