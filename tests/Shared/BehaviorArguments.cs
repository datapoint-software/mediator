using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datapoint.Mediator.Tests.Shared;

// Checks that every overload of a behaviour refuses a null request or a null next, before it does anything else.
internal static class BehaviorArguments
{
    public static void AssertNullsAreRefused<TCommandResult, TQueryResult, TStreamResult>(
        IBehavior behavior,
        ICommand command,
        ICommand<TCommandResult> commandWithResult,
        IQuery<TQueryResult> query,
        IStreamQuery<TStreamResult> streamQuery)
    {
        AssertRefused("command", () => behavior.HandleAsync((ICommand)null!, _ => default));
        AssertRefused("next", () => behavior.HandleAsync(command, null!));
        AssertRefused("command", () => behavior.HandleAsync((ICommand<TCommandResult>)null!, _ => default));
        AssertRefused("next", () => behavior.HandleAsync(commandWithResult, null!));
        AssertRefused("query", () => behavior.HandleAsync((IQuery<TQueryResult>)null!, _ => default));
        AssertRefused("next", () => behavior.HandleAsync(query, null!));
        AssertRefused("query", () => behavior.HandleAsync((IStreamQuery<TStreamResult>)null!, _ => AsyncEnumerable.Empty<TStreamResult>()));
        AssertRefused("next", () => behavior.HandleAsync(streamQuery, null!));
    }

    public static void AssertRefused(string paramName, Action call) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentNullException>(call).ParamName);
}
