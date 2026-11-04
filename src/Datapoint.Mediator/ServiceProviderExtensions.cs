using System;
using System.Collections.Generic;

namespace Datapoint.Mediator;

internal static class ServiceProviderExtensions
{
    public static T GetRequiredHandler<T>(this IServiceProvider serviceProvider, Type actionType)
        where T : class =>
        serviceProvider.GetService(typeof(T)) as T ?? throw HandlerNotFound(typeof(T), actionType);

    public static T[] GetAll<T>(this IServiceProvider serviceProvider) =>
        Services.GetAll<T>(serviceProvider, typeof(IEnumerable<T>));

    private static InvalidOperationException HandlerNotFound(Type handlerType, Type actionType) =>
        new($"No handler is registered for '{actionType}'. Register an implementation of '{handlerType}'.");
}
