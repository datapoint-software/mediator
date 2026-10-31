using System;
using System.Linq;
using System.Reflection;
using Datapoint.Mediator.DependencyInjection.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class AssemblyScanningTests
{
    private static readonly Assembly FixturesAssembly = typeof(FixtureCommand).Assembly;

    [Fact]
    public void AddHandlersFromAssembly_RegistersPublicAndInternalHandlers()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandlersFromAssembly(FixturesAssembly));

        Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<FixtureCommand>)
            && d.ImplementationType == typeof(FixtureCommandHandler));
        Assert.Single(services, d => d.ServiceType == typeof(IQueryHandler<FixtureQuery, int>)
            && d.ImplementationType?.Name == "InternalQueryHandler");
    }

    [Fact]
    public void AddHandlersFromAssembly_SkipsAbstractGenericAndBehaviorTypes()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandlersFromAssembly(FixturesAssembly));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ICommandHandler<SkippedCommand>));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IBehavior));
    }

    [Fact]
    public void AddHandlersFromAssembly_RegistersMessageHandlersInFullNameOrder()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandlersFromAssembly(FixturesAssembly));

        var handlers = services
            .Where(d => d.ServiceType == typeof(IMessageHandler<FixtureMessage>))
            .Select(d => d.ImplementationType);
        Assert.Equal([typeof(AlphaMessageHandler), typeof(MiddleMessageHandler), typeof(ZetaMessageHandler)], handlers);
    }

    [Fact]
    public void AddHandlersFromAssemblyContaining_MatchesAddHandlersFromAssembly()
    {
        var byAssembly = new ServiceCollection().AddMediator(o => o.AddHandlersFromAssembly(FixturesAssembly));
        var byType = new ServiceCollection().AddMediator(o => o.AddHandlersFromAssemblyContaining<FixtureCommand>());

        Assert.Equal(Describe(byAssembly), Describe(byType));
    }

    [Fact]
    public void AddHandlersFromAssembly_WithLifetime_AppliesItToEveryHandler()
    {
        var services = new ServiceCollection().AddMediator(o =>
            o.AddHandlersFromAssembly(FixturesAssembly, ServiceLifetime.Singleton));

        Assert.All(services.Where(d => d.ServiceType != typeof(IMediator)), d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));
    }

    [Fact]
    public void AddHandlersFromAssembly_NullAssembly_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddHandlersFromAssembly(null!));

        Assert.Equal("assembly", exception.ParamName);
    }

    private static (Type, Type?, ServiceLifetime)[] Describe(IServiceCollection services) =>
        services.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)).ToArray();
}
