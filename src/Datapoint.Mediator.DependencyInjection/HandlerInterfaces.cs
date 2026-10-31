using System;
using System.Collections.Generic;

namespace Datapoint.Mediator;

internal static class HandlerInterfaces
{
    // Returns the closed handler interfaces a type implements, such as ICommandHandler<CreateUser>.
    public static List<Type> Of(Type type)
    {
        var result = new List<Type>();

        foreach (var @interface in type.GetInterfaces())
        {
            if (@interface.IsGenericType && IsHandlerDefinition(@interface.GetGenericTypeDefinition()))
            {
                result.Add(@interface);
            }
        }

        return result;
    }

    public static bool IsMessageHandler(Type handlerInterface) =>
        handlerInterface.GetGenericTypeDefinition() == typeof(IMessageHandler<>);

    private static bool IsHandlerDefinition(Type definition) =>
        definition == typeof(ICommandHandler<>)
        || definition == typeof(ICommandHandler<,>)
        || definition == typeof(IQueryHandler<,>)
        || definition == typeof(IStreamQueryHandler<,>)
        || definition == typeof(IMessageHandler<>);
}
