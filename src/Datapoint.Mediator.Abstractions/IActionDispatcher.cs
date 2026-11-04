using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator;

/// <summary>
/// Dispatches actions by name, with their arguments as JSON, for hosts such as an API, a CLI or an MCP server.
/// </summary>
/// <remarks>
/// <para>
/// The arguments are deserialized into the action's type, which is then sent through <see cref="IMediator"/>, so every
/// behaviour runs, including authorization and validation.
/// </para>
/// <para>
/// The arguments are deserialized with the given <see cref="JsonSerializerOptions"/>, or
/// <see cref="JsonSerializerOptions.Web"/> when none are given. For Native AOT, use options whose
/// <see cref="JsonSerializerOptions.TypeInfoResolver"/> is a source-generated <c>JsonSerializerContext</c> that includes
/// every action type.
/// </para>
/// </remarks>
public interface IActionDispatcher
{
    /// <summary>
    /// Dispatches a command or query by name.
    /// </summary>
    /// <param name="name">The action's name.</param>
    /// <param name="arguments">
    /// The action's arguments, as a JSON object. An undefined element is treated as an empty object.
    /// </param>
    /// <param name="options">The options to deserialize the arguments with, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that completes with the action's result, or <see langword="null"/> for a command without a result. Await
    /// it only once.
    /// </returns>
    /// <exception cref="ActionNotFoundException">No action that isn't hidden has that name.</exception>
    /// <exception cref="InvalidActionArgumentsException">The arguments don't match the action's type.</exception>
    /// <exception cref="System.InvalidOperationException">The action is a stream query.</exception>
    ValueTask<object?> DispatchAsync(
        string name,
        JsonElement arguments,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a stream query by name.
    /// </summary>
    /// <param name="name">The action's name.</param>
    /// <param name="arguments">
    /// The action's arguments, as a JSON object. An undefined element is treated as an empty object.
    /// </param>
    /// <param name="options">The options to deserialize the arguments with, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the enumeration.</param>
    /// <returns>The query's results, produced as they are enumerated.</returns>
    /// <exception cref="ActionNotFoundException">No action that isn't hidden has that name.</exception>
    /// <exception cref="InvalidActionArgumentsException">The arguments don't match the action's type.</exception>
    /// <exception cref="System.InvalidOperationException">The action is not a stream query.</exception>
    IAsyncEnumerable<object?> StreamAsync(
        string name,
        JsonElement arguments,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);
}
