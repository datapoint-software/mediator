using System;
using Microsoft.Extensions.DependencyInjection;

namespace Datapoint.Mediator.Authorization;

// The one-accessor rule (ADR 0014). Linked as source into the packages that register an IPrincipalAccessor.
internal static class PrincipalAccessorRegistration
{
    // Returns true when isSame matches the registered accessor, so registering it again has no effect, and false when
    // none is registered. Throws when another accessor is registered.
    public static bool IsRegistered(IServiceCollection services, Func<ServiceDescriptor, bool> isSame, string accessorName)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IPrincipalAccessor) || descriptor.IsKeyedService)
            {
                continue;
            }

            if (isSame(descriptor))
            {
                return true;
            }

            throw new InvalidOperationException(
                $"Cannot register {accessorName}, because another {nameof(IPrincipalAccessor)} is already registered. " +
                "Register only one.");
        }

        return false;
    }
}
