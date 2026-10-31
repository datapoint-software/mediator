using System;
using System.Collections.Generic;

namespace Datapoint.Mediator.Tests.Fakes;

// Returns exactly what each test registers, so tests control resolution, including null and non-array enumerables.
public sealed class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object?> _services = [];

    public FakeServiceProvider Add<TService>(TService service)
        where TService : class =>
        Set(typeof(TService), service);

    public FakeServiceProvider AddAll<TService>(params TService[] services) =>
        Set(typeof(IEnumerable<TService>), services);

    public FakeServiceProvider Set(Type serviceType, object? service)
    {
        _services[serviceType] = service;
        return this;
    }

    public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
}
