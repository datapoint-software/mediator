using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class DuplicateHandlerTests
{
    [Fact]
    public void SecondCommandHandler_Throws_NamingBothHandlers()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddMediator(o => o.AddHandler<PingHandler>().AddHandler<OtherPingHandler>()));

        Assert.Contains(nameof(PingHandler), exception.Message);
        Assert.Contains(nameof(OtherPingHandler), exception.Message);
    }

    [Fact]
    public void SecondQueryHandler_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddMediator(o => o.AddHandler<EchoHandler>().AddHandler<OtherEchoHandler>()));
    }

    [Fact]
    public void SecondCommandHandler_InLaterAddMediatorCall_Throws()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<PingHandler>());

        Assert.Throws<InvalidOperationException>(() => services.AddMediator(o => o.AddHandler<OtherPingHandler>()));
    }

    [Fact]
    public void SameCommandHandlerTwice_IsRegisteredOnce()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<PingHandler>().AddHandler<PingHandler>());

        Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<Ping>));
    }

    [Fact]
    public void SameCommandHandlerRegisteredManuallyBefore_IsNotAddedAgain()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<Ping>, PingHandler>();

        services.AddMediator(o => o.AddHandler<PingHandler>());

        Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<Ping>));
    }

    [Fact]
    public void FactoryRegisteredBefore_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<Ping>>(_ => new OtherPingHandler());

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddMediator(o => o.AddHandler<PingHandler>()));

        Assert.Contains("a factory or instance", exception.Message);
    }

    [Fact]
    public void SeveralMessageHandlers_AreAllRegistered()
    {
        var services = new ServiceCollection().AddMediator(o =>
            o.AddHandler<FirstNotifiedHandler>().AddHandler<SecondNotifiedHandler>().AddHandler<FirstNotifiedHandler>());

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IMessageHandler<Notified>)));
    }
}
