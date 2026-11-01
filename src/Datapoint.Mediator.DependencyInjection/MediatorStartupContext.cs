using System;
using System.Collections.Generic;

namespace Datapoint.Mediator;

/// <summary>
/// What a startup check registered with <see cref="MediatorOptions.AddStartupCheck"/> can inspect.
/// </summary>
public sealed class MediatorStartupContext
{
    internal MediatorStartupContext(IReadOnlyCollection<Type> requestTypes)
    {
        RequestTypes = requestTypes;
    }

    /// <summary>
    /// Gets the types of every command, query and stream query with a registered handler, from every
    /// <c>AddMediator</c> call so far.
    /// </summary>
    public IReadOnlyCollection<Type> RequestTypes { get; }
}
