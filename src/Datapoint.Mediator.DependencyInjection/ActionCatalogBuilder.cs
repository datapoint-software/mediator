using System;
using System.Collections.Generic;

namespace Datapoint.Mediator;

// Accumulates the action types handled across AddMediator calls, and rebuilds the catalog eagerly on each call, so
// naming mistakes are found at startup rather than on first use.
internal sealed class ActionCatalogBuilder
{
    private readonly List<Type> _actionTypes = [];

    public ActionCatalog Catalog { get; private set; } = new([]);

    public void Add(IEnumerable<Type> actionTypes)
    {
        List<Type> combined = [.. _actionTypes, .. actionTypes];
        Catalog = new ActionCatalog(combined);
        _actionTypes.Clear();
        _actionTypes.AddRange(combined);
    }
}
