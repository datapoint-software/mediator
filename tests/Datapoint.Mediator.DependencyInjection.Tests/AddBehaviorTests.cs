using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class AddBehaviorTests
{
    [Fact]
    public void AddBehavior_PreservesRegistrationOrder()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddBehavior<InnerBehavior>().AddBehavior<OuterBehavior>());

        Assert.Equal([typeof(InnerBehavior), typeof(OuterBehavior)], BehaviorTypes(services));
    }

    [Fact]
    public void AddBehavior_SameBehaviorTwice_IsRegisteredOnce()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddBehavior<OuterBehavior>().AddBehavior<OuterBehavior>());

        Assert.Equal([typeof(OuterBehavior)], BehaviorTypes(services));
    }

    [Fact]
    public void AddBehavior_AppliesDefaultAndExplicitLifetimes()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Transient;
            o.AddBehavior<OuterBehavior>().AddBehavior<InnerBehavior>(ServiceLifetime.Singleton);
        });

        Assert.Equal(
            [ServiceLifetime.Transient, ServiceLifetime.Singleton],
            services.Where(d => d.ServiceType == typeof(IBehavior)).Select(d => d.Lifetime));
    }

    [Fact]
    public void AddBehavior_NullType_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddBehavior(null!));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    [Theory]
    [InlineData(typeof(NotAHandler))]
    [InlineData(typeof(AbstractBehavior))]
    [InlineData(typeof(IBehavior))]
    public void AddBehavior_InvalidType_Throws(Type behaviorType)
    {
        var exception = Assert.Throws<ArgumentException>(() => new MediatorOptions().AddBehavior(behaviorType));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    private static Type?[] BehaviorTypes(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IBehavior)).Select(d => d.ImplementationType).ToArray();
}
