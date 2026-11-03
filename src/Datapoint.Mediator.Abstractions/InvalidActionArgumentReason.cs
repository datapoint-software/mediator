namespace Datapoint.Mediator;

/// <summary>
/// Why an argument named by <see cref="InvalidActionArgumentsException.Field"/> couldn't be read.
/// </summary>
public enum InvalidActionArgumentReason
{
    /// <summary>
    /// The value has the wrong type or format, or the argument can't be read for another reason.
    /// </summary>
    Invalid,

    /// <summary>
    /// A required argument is missing.
    /// </summary>
    Missing,

    /// <summary>
    /// The value is JSON <c>null</c>, and the argument doesn't accept it.
    /// </summary>
    Null,
}
