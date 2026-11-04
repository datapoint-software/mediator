using System;
using System.Collections.Generic;
using System.Linq;

namespace Datapoint.Mediator;

// Resolves every registration of a service as an array. Linked as source into the mediator and the behaviour packages.
internal static class Services
{
    // enumerableType is IEnumerable<X>, where X is T or implements it, such as IEnumerable<IValidator<Order>> for IValidator.
    public static T[] GetAll<T>(IServiceProvider serviceProvider, Type enumerableType)
    {
        // Containers other than Microsoft's may return null or a non-array enumerable.
        switch (serviceProvider.GetService(enumerableType))
        {
            case T[] array:
                return array;
            case IEnumerable<T> enumerable:
                return enumerable.ToArray();
            default:
                return [];
        }
    }
}
