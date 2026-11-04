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
    public void AddBehavior_ByTypeWithLifetime_OverridesDefaultLifetime()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddBehavior(typeof(OuterBehavior), ServiceLifetime.Singleton));

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == typeof(IBehavior)).Lifetime);
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
    public void AddBehavior_GenericInvalidType_NamesTheTypeParameter()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MediatorOptions().AddBehavior<AbstractBehavior>());

        Assert.Equal("TBehavior", exception.ParamName);
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
    public void AddBehaviorWithFactory_ConflictingWithAnotherRegistration_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddBehavior<OuterBehavior>()
            .AddBehavior(_ => new OuterBehavior(new Recorder()))));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddBehavior(_ => new OuterBehavior(new Recorder()))
            .AddBehavior<OuterBehavior>()));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddBehavior(_ => new OuterBehavior(new Recorder()))
            .AddBehavior(_ => new OuterBehavior(new Recorder()))));
    }

    [Fact]
    public void AddBehaviorWithFactory_ConflictingAcrossAddMediatorCalls_Throws()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddBehavior(_ => new OuterBehavior(new Recorder())));

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddMediator(o => o.AddBehavior<OuterBehavior>()));

        Assert.Contains(nameof(OuterBehavior), exception.Message);
    }

    [Fact]
    public void AddBehavior_AlreadyRegisteredAsAnInstance_HasNoEffect()
    {
        var services = new ServiceCollection().AddSingleton<IBehavior>(new OuterBehavior(new Recorder()));

        services.AddMediator(o => o.AddBehavior<OuterBehavior>());

        Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
    }

    [Fact]
    public void AddBehaviorWithFactory_DifferentTypes_AreBothRegistered()
    {
        var services = new ServiceCollection().AddMediator(o => o
            .AddBehavior(_ => new OuterBehavior(new Recorder()))
            .AddBehavior(_ => new InnerBehavior(new Recorder())));

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IBehavior)));
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
