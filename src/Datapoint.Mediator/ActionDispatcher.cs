using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Dispatchers;

namespace Datapoint.Mediator;

/// <summary>
/// Dispatches actions by name, with their arguments as JSON, through an <see cref="IMediator"/>.
/// </summary>
/// <remarks>
/// Create the dispatcher from the current scope, like the mediator it uses, for example by registering it as a scoped
/// service.
/// </remarks>
public sealed class ActionDispatcher : IActionDispatcher
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly IMediator _mediator;
    private readonly IActionCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDispatcher"/> class.
    /// </summary>
    /// <param name="mediator">The mediator to send actions through.</param>
    /// <param name="catalog">The actions that can be dispatched.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mediator"/> or <paramref name="catalog"/> is <see langword="null"/>.
    /// </exception>
    public ActionDispatcher(IMediator mediator, IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(catalog);
        _mediator = mediator;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public ValueTask<object?> DispatchAsync(
        string name,
        JsonElement arguments,
        JsonSerializerOptions? options = null,
        CancellationToken ct = default)
    {
        var (descriptor, action) = Resolve(name, arguments, options);
        return ActionInvoker.Get(descriptor).InvokeAsync(_mediator, action, ct);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<object?> StreamAsync(
        string name,
        JsonElement arguments,
        JsonSerializerOptions? options = null,
        CancellationToken ct = default)
    {
        var (descriptor, action) = Resolve(name, arguments, options);
        return ActionInvoker.Get(descriptor).StreamAsync(_mediator, action, ct);
    }

    private (ActionDescriptor Descriptor, object Action) Resolve(string name, JsonElement arguments, JsonSerializerOptions? options)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_catalog.TryGetAction(name, out var descriptor))
        {
            throw new ActionNotFoundException();
        }

        if (arguments.ValueKind == JsonValueKind.Undefined)
        {
            arguments = EmptyObject;
        }

        object? action;

        try
        {
            action = arguments.Deserialize(descriptor.ActionType, options ?? JsonSerializerOptions.Web);
        }
        catch (JsonException exception)
        {
            throw new InvalidActionArgumentsException($"The arguments for action '{descriptor.Name}' are not valid.", exception);
        }

        return action is null
            ? throw new InvalidActionArgumentsException($"The arguments for action '{descriptor.Name}' are missing.")
            : (descriptor, action);
    }
}
