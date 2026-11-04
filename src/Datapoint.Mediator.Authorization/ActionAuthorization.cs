using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            // Never null: Activator.CreateInstance returns null only for Nullable<T>, and this type is a class.
            static type => (ActionAuthorization)Activator.CreateInstance(typeof(ActionAuthorization<>).MakeGenericType(type))!);

    // Adds each authorizer's decision to the list, so the caller keeps them when an authorizer throws. The list stays
    // empty when the action has no authorizer.
    public abstract ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        List<AuthorizerDecision> decisions,
        CancellationToken cancellationToken);
}

internal sealed class ActionAuthorization<TAction> : ActionAuthorization
{
    public override async ValueTask AuthorizeAsync(
        object action,
        ClaimsPrincipal principal,
        IServiceProvider serviceProvider,
        List<AuthorizerDecision> decisions,
        CancellationToken cancellationToken)
    {
        var authorizers = Services.GetAll<IAuthorizer<TAction>>(serviceProvider, typeof(IEnumerable<IAuthorizer<TAction>>));

        var typedAction = (TAction)action;

        foreach (var authorizer in authorizers)
        {
            // A null decision, from a careless implementation, is treated as a denial.
            var decision = await authorizer.AuthorizeAsync(typedAction, principal, cancellationToken).ConfigureAwait(false)
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
