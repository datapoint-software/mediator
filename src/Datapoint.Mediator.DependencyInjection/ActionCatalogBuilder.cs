using System;
using System.Collections.Generic;
using System.Linq;

namespace Datapoint.Mediator;

// Accumulates the action types handled across AddMediator calls, and rebuilds the catalog eagerly on each call, so
// naming mistakes are found at startup rather than on first use.
internal sealed class ActionCatalogBuilder
{
    private readonly List<Type> _handledTypes = [];
    private readonly List<Action<MediatorStartupContext>> _startupChecks = [];

    public ActionCatalog Catalog { get; private set; } = new([]);

    public void Add(IEnumerable<Type> actionTypes)
    {
        List<Type> combined = [.. _handledTypes, .. actionTypes];
        Catalog = new ActionCatalog(combined);
        _handledTypes.Clear();
        _handledTypes.AddRange(combined);
    }

    // Runs every check registered so far, by any AddMediator call, over every command, query and stream query so far.
    public void Check(IEnumerable<Action<MediatorStartupContext>> startupChecks)
    {
        _startupChecks.AddRange(startupChecks);

        var context = new MediatorStartupContext(
            _handledTypes.Where(type => !typeof(IMessage).IsAssignableFrom(type)).Distinct().ToArray());

        foreach (var check in _startupChecks)
        {
            check(context);
        }
    }
}
