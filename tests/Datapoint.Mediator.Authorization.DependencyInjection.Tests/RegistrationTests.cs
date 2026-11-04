using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.DependencyInjection.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void AddAuthorizationBehavior_NullOptions_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((MediatorOptions)null!).AddAuthorizationBehavior());

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void AddAuthorizationBehavior_RegistersScopedBehaviorOnce_EvenWhenDefaultLifetimeIsSingleton()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Singleton;
            Assert.Same(o, o.AddAuthorizationBehavior().AddAuthorizationBehavior());
        });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.Equal(typeof(AuthorizationBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAuthorizationBehavior_KeepsItsPositionAmongBehaviors()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddAuthorizationBehavior().AddBehavior<OtherBehavior>());

        Assert.Equal(
            [typeof(AuthorizationBehavior), typeof(OtherBehavior)],
            services.Where(d => d.ServiceType == typeof(IBehavior)).Select(d => d.ImplementationType));
    }

    [Fact]
    public void AddAuthorizationBehaviorWithSettings_NullArguments_Throw()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(
            () => ((MediatorOptions)null!).AddAuthorizationBehavior(_ => { })).ParamName);
        Assert.Equal("configure", Assert.Throws<ArgumentNullException>(
            () => new MediatorOptions().AddAuthorizationBehavior(null!)).ParamName);
    }

    [Fact]
    public void AddAuthorizationBehaviorWithSettings_RegistersAScopedFactory()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddAuthorizationBehavior(a => a.RequireTransaction = TransactionRequirement.Commands));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<AuthorizationBehavior>(provider.CreateScope().ServiceProvider.GetRequiredService<IBehavior>());
    }

    [Fact]
    public void AddAuthorizationBehavior_WithAndWithoutSettings_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddAuthorizationBehavior()
            .AddAuthorizationBehavior(_ => { })));
    }

    [Fact]
    public void AddAuthorizationBehaviorWithSettings_InvalidSetting_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MediatorOptions().AddAuthorizationBehavior(a => a.RequireTransaction = (TransactionRequirement)9));
    }

    [Fact]
    public void AddAuthenticationBehavior_RegistersScopedBehaviorOnce()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Singleton;
            Assert.Same(o, o.AddAuthenticationBehavior().AddAuthenticationBehavior());
        });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.Equal(typeof(AuthenticationBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => ((MediatorOptions)null!).AddAuthenticationBehavior()).ParamName);
    }

    [Fact]
    public void AddAuthorizationContext_NullServices_Throws()
    {
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddAuthorizationContext()).ParamName);
    }

    [Fact]
    public void AddAuthorizationContext_RegistersOneSingleton()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddAuthorizationContext().AddAuthorizationContext());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IAuthorizationContext));
        Assert.Equal(typeof(AuthorizationContext), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddAuthorizationObserver_NullServices_Throws()
    {
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddAuthorizationObserver<AuditObserver>()).ParamName);
    }

    [Fact]
    public void AddAuthorizationObserver_RegistersEachObserverOnce_WithTheGivenLifetime()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services
            .AddAuthorizationObserver<AuditObserver>()
            .AddAuthorizationObserver<AuditObserver>()
            .AddAuthorizationObserver<MetricsObserver>(ServiceLifetime.Singleton));

        var descriptors = services.Where(d => d.ServiceType == typeof(IAuthorizationObserver)).ToArray();
        Assert.Equal([typeof(AuditObserver), typeof(MetricsObserver)], descriptors.Select(d => d.ImplementationType));
        Assert.Equal([ServiceLifetime.Scoped, ServiceLifetime.Singleton], descriptors.Select(d => d.Lifetime));
    }

    [Fact]
    public void AddAuthorizersFromAssembly_NullArguments_Throw()
    {
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddAuthorizersFromAssembly(typeof(RegistrationTests).Assembly)).ParamName);
        Assert.Equal("assembly", Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddAuthorizersFromAssembly(null!)).ParamName);
    }

    [Fact]
    public void AddAuthorizersFromAssembly_RegistersConcreteAuthorizersInNameOrder()
    {
        var services = new ServiceCollection().AddAuthorizersFromAssemblyContaining<RegistrationTests>();

        Assert.Equal(
            ["AlphaApproveAuthorizer", "InvoiceAuthorizer", "ZetaApproveAuthorizer"],
            services.Where(d => d.ServiceType == typeof(IAuthorizer<ApproveInvoice>)).Select(d => d.ImplementationType!.Name));
        Assert.Equal(
            [typeof(InvoiceAuthorizer)],
            services.Where(d => d.ServiceType == typeof(IAuthorizer<ArchiveInvoice>)).Select(d => d.ImplementationType));
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    [Fact]
    public void AddAuthorizersFromAssembly_CalledTwice_AddsNothingTheSecondTime()
    {
        var services = new ServiceCollection().AddAuthorizersFromAssemblyContaining<RegistrationTests>();
        var count = services.Count;

        services.AddAuthorizersFromAssembly(typeof(RegistrationTests).Assembly);

        Assert.Equal(count, services.Count);
    }

    [Fact]
    public void AddAuthorizersFromAssembly_WithLifetime_AppliesIt()
    {
        var services = new ServiceCollection().AddAuthorizersFromAssembly(typeof(RegistrationTests).Assembly, ServiceLifetime.Transient);

        Assert.All(services, d => Assert.Equal(ServiceLifetime.Transient, d.Lifetime));
    }

    [Fact]
    public void AddAuthorizersFromAssemblyContaining_WithLifetime_AppliesIt()
    {
        var services = new ServiceCollection().AddAuthorizersFromAssemblyContaining<RegistrationTests>(ServiceLifetime.Singleton);

        Assert.NotEmpty(services);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));
    }
}
