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
}
