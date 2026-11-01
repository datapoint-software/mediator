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

    [Fact]
    public void AddBehaviorWithFactory_CreatesTheBehaviorFromTheScope_InItsPosition()
    {
        var recorder = new Recorder();
        Recorder? usedRecorder = null;
        using var provider = new ServiceCollection()
            .AddSingleton(recorder)
            .AddMediator(o => o
                .AddBehavior<InnerBehavior>()
                .AddBehavior(sp =>
                {
                    usedRecorder = sp.GetRequiredService<Recorder>();
                    return new OuterBehavior(usedRecorder);
                }, ServiceLifetime.Singleton))
            .BuildServiceProvider();

        var behaviors = provider.CreateScope().ServiceProvider.GetServices<IBehavior>().ToArray();

        Assert.IsType<InnerBehavior>(behaviors[0]);
        Assert.IsType<OuterBehavior>(behaviors[1]);
        Assert.Same(recorder, usedRecorder);
    }

    [Fact]
    public void AddBehaviorWithFactory_UsesTheDefaultLifetime()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Transient;
            o.AddBehavior(sp => new OuterBehavior(new Recorder()));
        });

        Assert.Equal(ServiceLifetime.Transient, Assert.Single(services, d => d.ServiceType == typeof(IBehavior)).Lifetime);
    }

    [Fact]
    public void AddBehaviorWithFactory_SameTypeAgain_KeepsTheFirstRegistration()
    {
        var services = new ServiceCollection().AddMediator(o => o
            .AddBehavior<OuterBehavior>()
            .AddBehavior(_ => new OuterBehavior(new Recorder()))
            .AddBehavior(_ => new InnerBehavior(new Recorder()))
            .AddBehavior<InnerBehavior>());

        var descriptors = services.Where(d => d.ServiceType == typeof(IBehavior)).ToArray();
        Assert.Equal(2, descriptors.Length);
        Assert.Equal(typeof(OuterBehavior), descriptors[0].ImplementationType);
        Assert.NotNull(descriptors[1].ImplementationFactory);
    }

    [Fact]
    public void AddBehaviorWithFactory_NullFactory_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddBehavior<OuterBehavior>((Func<IServiceProvider, OuterBehavior>)null!));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public void AddBehaviorWithFactory_NotAConcreteType_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MediatorOptions().AddBehavior<IBehavior>(_ => new OuterBehavior(new Recorder())));

        Assert.Equal("TBehavior", exception.ParamName);
    }

    private static Type?[] BehaviorTypes(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IBehavior)).Select(d => d.ImplementationType).ToArray();
}
