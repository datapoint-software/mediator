using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

// Shared by AuthenticationBehavior and AuthorizationBehavior: anonymous actions, the current user, and observers.
internal static class Authentication
{
    private static readonly ConcurrentDictionary<Type, bool> AnonymousActions = new();

    public static bool IsAnonymous(Type actionType) =>
        AnonymousActions.GetOrAdd(actionType, static type => type.IsDefined(typeof(AllowAnonymousAccessAttribute), inherit: false));

    // Returns the authenticated user, or observes the failure and throws AuthenticationRequiredException.
    public static async ValueTask<ClaimsPrincipal> RequirePrincipalAsync(
        IServiceProvider serviceProvider,
        object action,
        IAuthorizationObserver[] observers,
        CancellationToken ct)
    {
        var accessor = serviceProvider.GetService(typeof(IPrincipalAccessor)) as IPrincipalAccessor
            ?? throw new InvalidOperationException(
                $"No '{typeof(IPrincipalAccessor)}' is registered. Register one that returns the current user.");

        var principal = accessor.Principal;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            await ObserveAsync(observers, action, principal, AuthorizationOutcome.AuthenticationRequired, null, null, ct)
                .ConfigureAwait(false);

            throw new AuthenticationRequiredException($"Authentication is required to run '{action.GetType()}'.");
        }

        return principal;
    }

    public static IAuthorizationObserver[] GetObservers(IServiceProvider serviceProvider) =>
        // Containers other than Microsoft's may return null or a non-array enumerable.
        serviceProvider.GetService(typeof(IEnumerable<IAuthorizationObserver>)) switch
        {
            IAuthorizationObserver[] array => array,
            IEnumerable<IAuthorizationObserver> enumerable => enumerable.ToArray(),
            _ => [],
        };

    // Every observer runs, in order. If one throws, the action doesn't run: it can't be audited.
    public static async ValueTask ObserveAsync(
        IAuthorizationObserver[] observers,
        object action,
        ClaimsPrincipal? principal,
        AuthorizationOutcome outcome,
        AuthorizationResult? result,
        Exception? exception,
        CancellationToken ct)
    {
        if (observers.Length == 0)
        {
            return;
        }

        var authorization = new AuthorizationEvent(action, principal, outcome, result, exception);

        foreach (var observer in observers)
        {
            await observer.OnAuthorizedAsync(authorization, ct).ConfigureAwait(false);
        }
    }
}
