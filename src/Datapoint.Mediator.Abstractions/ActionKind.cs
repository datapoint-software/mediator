namespace Datapoint.Mediator;

/// <summary>
/// The kind of an action, which determines how it is dispatched.
/// </summary>
public enum ActionKind
{
    /// <summary>
    /// An <see cref="ICommand"/>, which produces no result.
    /// </summary>
    Command,

    /// <summary>
    /// An <see cref="ICommand{TResult}"/>, which produces a result.
    /// </summary>
    CommandWithResult,

    /// <summary>
    /// An <see cref="IQuery{TResult}"/>.
    /// </summary>
    Query,

    /// <summary>
    /// An <see cref="IStreamQuery{TResult}"/>, which produces its results as a stream.
    /// </summary>
    StreamQuery,
}
