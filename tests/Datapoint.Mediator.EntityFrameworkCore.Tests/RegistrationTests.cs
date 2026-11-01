using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.EntityFrameworkCore.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void AddTransactionBehavior_NullOptions_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((MediatorOptions)null!).AddTransactionBehavior<AppDbContext>());

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void AddTransactionBehavior_RegistersScopedBehaviorOnce_EvenWhenDefaultLifetimeIsSingleton()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Singleton;
            Assert.Same(o, o.AddTransactionBehavior<AppDbContext>().AddTransactionBehavior<AppDbContext>());
        });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.Equal(typeof(TransactionBehavior<AppDbContext>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddTransactionBehaviorWithSettings_NullArguments_Throw()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(
            () => ((MediatorOptions)null!).AddTransactionBehavior<AppDbContext>(_ => { })).ParamName);
        Assert.Equal("configure", Assert.Throws<ArgumentNullException>(
            () => new MediatorOptions().AddTransactionBehavior<AppDbContext>(null!)).ParamName);
    }

    [Fact]
    public void AddTransactionBehaviorWithSettings_RegistersAScopedFactory()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddTransactionBehavior<AppDbContext>(t => t.Queries = QueryTransactions.ReadOnly));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddTransactionBehaviorWithSettings_InvalidSetting_ThrowsWhenRegistering()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MediatorOptions().AddTransactionBehavior<AppDbContext>(t => t.IsolationLevel = (System.Data.IsolationLevel)12345));
    }

    [Fact]
    public void AddTransactionBehavior_WithAndWithoutSettings_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddTransactionBehavior<AppDbContext>()
            .AddTransactionBehavior<AppDbContext>(t => t.Queries = QueryTransactions.ReadOnly)));
    }

    [Fact]
    public void AddTransactionContext_RegistersOneSingleton()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddTransactionContext().AddTransactionContext());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ITransactionContext));
        Assert.Equal(typeof(TransactionContext), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddTransactionContext()).ParamName);
    }

    [Fact]
    public void AddTransactionInitializer_RegistersEachOnce_WithTheGivenLifetime()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services
            .AddTransactionInitializer<AppDbContext, RecordingInitializer>()
            .AddTransactionInitializer<AppDbContext, RecordingInitializer>(ServiceLifetime.Transient));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ITransactionInitializer<AppDbContext>));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddTransactionInitializer<AppDbContext, RecordingInitializer>()).ParamName);
    }

    [Fact]
    public void TransactionInitialization_RequiresARequest()
    {
        Assert.Equal("request", Assert.Throws<ArgumentNullException>(
            () => new TransactionInitialization(null!, false, false, 1, Guid.NewGuid())).ParamName);
    }
}
