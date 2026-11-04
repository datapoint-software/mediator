using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class AddHandlerTests
{
    [Fact]
    public void AddHandler_RegistersEveryHandlerInterface()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<MultiHandler>());

        var serviceTypes = services.Where(d => d.ImplementationType == typeof(MultiHandler)).Select(d => d.ServiceType);
        Assert.Equal(
            [typeof(ICommandHandler<MultiCommand>), typeof(IMessageHandler<Notified>), typeof(IQueryHandler<MultiQuery, int>)],
            serviceTypes.OrderBy(t => t.Name));
    }

    [Fact]
    public void AddHandler_ByType_RegistersHandler()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler(typeof(AddHandler)));

        Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<Add, int>) && d.ImplementationType == typeof(AddHandler));
    }

    [Fact]
    public void AddHandler_StreamQueryHandler_IsRegistered()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<CountToHandler>());

        Assert.Single(services, d => d.ServiceType == typeof(IStreamQueryHandler<CountTo, int>)
            && d.ImplementationType == typeof(CountToHandler));
    }

    [Fact]
    public void AddHandler_WithoutLifetime_UsesScopedByDefault()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<PingHandler>());

        Assert.Equal(ServiceLifetime.Scoped, LifetimeOf<ICommandHandler<Ping>>(services));
    }

    [Fact]
    public void AddHandler_WithoutLifetime_UsesDefaultLifetimeEvenWhenSetAfterwards()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.AddHandler<PingHandler>();
            o.DefaultLifetime = ServiceLifetime.Transient;
        });

        Assert.Equal(ServiceLifetime.Transient, LifetimeOf<ICommandHandler<Ping>>(services));
    }

    [Fact]
    public void AddHandler_WithLifetime_OverridesDefaultLifetime()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Transient;
            o.AddHandler<PingHandler>(ServiceLifetime.Singleton);
        });

        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf<ICommandHandler<Ping>>(services));
    }

    [Fact]
    public void AddHandler_ByTypeWithLifetime_OverridesDefaultLifetime()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler(typeof(PingHandler), ServiceLifetime.Singleton));

        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf<ICommandHandler<Ping>>(services));
    }

    [Fact]
    public void AddHandler_NullType_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddHandler(null!));

        Assert.Equal("handlerType", exception.ParamName);
    }

    [Theory]
    [InlineData(typeof(NotAHandler))]
    [InlineData(typeof(AbstractHandler))]
    [InlineData(typeof(GenericHandler<>))]
    [InlineData(typeof(ICommandHandler<Ping>))]
    public void AddHandler_InvalidType_Throws(Type handlerType)
    {
        var exception = Assert.Throws<ArgumentException>(() => new MediatorOptions().AddHandler(handlerType));

        Assert.Equal("handlerType", exception.ParamName);
    }

    private static ServiceLifetime LifetimeOf<TService>(IServiceCollection services) =>
        Assert.Single(services, d => d.ServiceType == typeof(TService)).Lifetime;
}
