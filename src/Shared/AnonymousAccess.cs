using System;

namespace Datapoint.Mediator.Authorization;

// The rule for an action that needs no authentication or authorization. Linked as source into the authorization packages,
// so the behaviours and the startup checks agree.
internal static class AnonymousAccess
{
    public static bool IsAllowed(Type actionType) =>
        actionType.IsDefined(typeof(AllowAnonymousAccessAttribute), inherit: false);
}
