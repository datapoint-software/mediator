using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datapoint.Mediator;

/// <summary>
/// Lists the actions that hosts can dispatch by name.
/// </summary>
/// <remarks>
/// Only actions marked with <see cref="ActionAttribute"/>, not hidden, and with a registered handler are listed.
/// </remarks>
public interface IActionCatalog
{
    /// <summary>
    /// Gets the actions that hosts can dispatch, ordered by name.
    /// </summary>
    IReadOnlyList<ActionDescriptor> Actions { get; }

    /// <summary>
    /// Gets the action with the specified name.
    /// </summary>
    /// <param name="name">The action's name.</param>
    /// <param name="action">The action, when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an action that isn't hidden has that name; otherwise, <see langword="false"/>.</returns>
    bool TryGetAction(string name, [NotNullWhen(true)] out ActionDescriptor? action);
}
