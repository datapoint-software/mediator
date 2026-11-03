using System;
using System.Text.Json;

namespace Datapoint.Mediator;

/// <summary>
/// Deserializes an action's JSON arguments, the way <see cref="ActionDispatcher"/> does.
/// </summary>
/// <remarks>
/// Use it to check arguments before sending them elsewhere, for example in a CLI that dispatches to a remote server,
/// so a local refusal names the same <see cref="InvalidActionArgumentsException.Field"/> as the server's.
/// </remarks>
public static class ActionArguments
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>
    /// Deserializes the arguments into an instance of the action's type.
    /// </summary>
    /// <param name="action">The action whose arguments these are.</param>
    /// <param name="arguments">
    /// The arguments, as a JSON object. An undefined element, such as <see langword="default"/>, is treated as
    /// <c>{}</c>.
    /// </param>
    /// <param name="options">
    /// The options to deserialize with, or <see langword="null"/> for <see cref="JsonSerializerOptions.Web"/>.
    /// </param>
    /// <returns>The action, never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidActionArgumentsException">
    /// The arguments don't match the action's type, or are JSON <c>null</c>. The exception's
    /// <see cref="InvalidActionArgumentsException.Field"/> names the argument that failed, when it can be determined.
    /// </exception>
    public static object Deserialize(ActionDescriptor action, JsonElement arguments, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        options ??= JsonSerializerOptions.Web;

        if (arguments.ValueKind == JsonValueKind.Undefined)
        {
            arguments = EmptyObject;
        }

        object? result;

        try
        {
            result = arguments.Deserialize(action.ActionType, options);
        }
        catch (JsonException exception)
        {
            var field = ArgumentField.Find(exception, arguments, options.GetTypeInfo(action.ActionType));
            var message = field is null
                ? $"The arguments for action '{action.Name}' are not valid."
                : $"The argument '{field}' of action '{action.Name}' is not valid.";

            throw new InvalidActionArgumentsException(message, action.Name, field, exception);
        }

        return result
            ?? throw new InvalidActionArgumentsException($"The arguments for action '{action.Name}' are missing.", action.Name, null, null);
    }
}
