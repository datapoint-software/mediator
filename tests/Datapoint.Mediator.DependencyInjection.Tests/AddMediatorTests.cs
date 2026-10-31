using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class AddMediatorTests
{
    [Fact]
    public void AddMediator_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMediator(_ => { }));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddMediatorWithoutCallback_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMediator());

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddMediator_NullConfigure_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMediator(null!));

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void AddMediator_RegistersScopedMediator()
    {
        var services = new ServiceCollection().AddMediator(_ => { });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMediator));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(Mediator), descriptor.ImplementationType);
    }

    [Fact]
    public void AddMediatorWithoutCallback_RegistersScopedMediator()
    {
        var services = new ServiceCollection().AddMediator();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMediator));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddMediator_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddMediator(_ => { }));
    }

    [Fact]
    public void AddMediator_CalledTwice_AddsNothingTheSecondTime()
    {
        var services = new ServiceCollection();
        static void Configure(MediatorOptions options) =>
            options.AddHandler<PingHandler>().AddHandler<FirstNotifiedHandler>().AddBehavior<OuterBehavior>();

        services.AddMediator(Configure);
        var expected = services.ToList();
        services.AddMediator(Configure);

        Assert.Equal(expected, services);
    }
}
