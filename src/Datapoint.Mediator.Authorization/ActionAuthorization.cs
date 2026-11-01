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

    // Adds each authorizer's decision to the list, so the caller keeps them when an authorizer throws. The list stays
    // empty when the action has no authorizer.
    public abstract ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        List<AuthorizerDecision> decisions,
        CancellationToken ct);
}

internal sealed class ActionAuthorization<TAction> : ActionAuthorization
{
    public override async ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        List<AuthorizerDecision> decisions,
        CancellationToken ct)
    {
        // Containers other than Microsoft's may return null or a non-array enumerable.
        var authorizers = serviceProvider.GetService(typeof(IEnumerable<IAuthorizer<TAction>>)) switch
        {
            IAuthorizer<TAction>[] array => array,
            IEnumerable<IAuthorizer<TAction>> enumerable => enumerable.ToArray(),
            _ => [],
        };

        var typedAction = (TAction)action;

        foreach (var authorizer in authorizers)
        {
            // A null decision, from a careless implementation, is treated as a denial.
            var decision = await authorizer.AuthorizeAsync(typedAction, principal, ct).ConfigureAwait(false)
                ?? AuthorizationDecision.Deny();

            decisions.Add(new AuthorizerDecision(authorizer.GetType(), decision));

            // Stop at the first denial, so a refused action causes no further queries.
            if (!decision.IsAllowed)
            {
                break;
            }
        }
    }
}
