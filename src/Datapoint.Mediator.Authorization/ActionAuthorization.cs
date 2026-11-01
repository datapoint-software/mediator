using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

// Runs the authorizers of one action type. Created once per action type and cached.
internal abstract class ActionAuthorization
{
    private static readonly ConcurrentDictionary<Type, ActionAuthorization> Cache = new();

    public static ActionAuthorization Get(Type actionType) =>
        Cache.GetOrAdd(
            actionType,
            static type => (ActionAuthorization)Activator.CreateInstance(typeof(ActionAuthorization<>).MakeGenericType(type))!);

    public abstract ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        CancellationToken ct);
}

internal sealed class ActionAuthorization<TAction> : ActionAuthorization
{
    public override async ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        // Containers other than Microsoft's may return null or a non-array enumerable.
        var authorizers = serviceProvider.GetService(typeof(IEnumerable<IAuthorizer<TAction>>)) switch
        {
            IAuthorizer<TAction>[] array => array,
            IEnumerable<IAuthorizer<TAction>> enumerable => enumerable.ToArray(),
            _ => [],
        };

        // Fail closed: an action that nobody wrote a rule for is not allowed.
        if (authorizers.Length == 0)
        {
            throw new AccessDeniedException($"No authorizer is registered for '{typeof(TAction)}', so access is denied.");
        }

        var typedAction = (TAction)action;

        foreach (var authorizer in authorizers)
        {
            // A null decision, from a careless implementation, is treated as a denial.
            var decision = await authorizer.AuthorizeAsync(typedAction, principal, ct).ConfigureAwait(false);

            if (decision is not { IsAllowed: true })
            {
                throw new AccessDeniedException($"Access to '{typeof(TAction)}' is denied.");
            }
        }
    }
}
