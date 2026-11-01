using System;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.DependencyInjection.Tests;

public sealed class PrincipalAccessorRegistrationTests
{
    [Fact]
    public void AddFixedPrincipalAccessor_RegistersSingletonWithThePrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var services = new ServiceCollection().AddFixedPrincipalAccessor(principal);

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(IPrincipalAccessor), descriptor.ServiceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Same(principal, provider.GetRequiredService<IPrincipalAccessor>().Principal);
    }

    [Fact]
    public void AddFixedPrincipalAccessor_WithoutUser_RegistersNullPrincipal()
    {
        using var provider = new ServiceCollection().AddFixedPrincipalAccessor(null).BuildServiceProvider();

        Assert.Null(provider.GetRequiredService<IPrincipalAccessor>().Principal);
    }

    [Fact]
    public void AddFixedPrincipalAccessor_SamePrincipalTwice_RegistersOnce()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var services = new ServiceCollection().AddFixedPrincipalAccessor(principal).AddFixedPrincipalAccessor(principal);

        Assert.Single(services);
    }

    [Fact]
    public void AddFixedPrincipalAccessor_DifferentPrincipal_Throws()
    {
        var services = new ServiceCollection().AddFixedPrincipalAccessor(new ClaimsPrincipal(new ClaimsIdentity("test")));

        Assert.Throws<InvalidOperationException>(
            () => services.AddFixedPrincipalAccessor(new ClaimsPrincipal(new ClaimsIdentity("test"))));
    }

    [Fact]
    public void AddFixedPrincipalAccessor_OtherAccessorRegistered_Throws()
    {
        var services = new ServiceCollection().AddScoped<IPrincipalAccessor, OtherPrincipalAccessor>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddFixedPrincipalAccessor(null));

        Assert.Contains("another IPrincipalAccessor is already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddFixedPrincipalAccessor_IgnoresKeyedAccessors()
    {
        var services = new ServiceCollection().AddKeyedScoped<IPrincipalAccessor, OtherPrincipalAccessor>("other");

        services.AddFixedPrincipalAccessor(null);

        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void AddFixedPrincipalAccessor_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddFixedPrincipalAccessor(null));

        Assert.Equal("services", exception.ParamName);
    }

    private sealed class OtherPrincipalAccessor : IPrincipalAccessor
    {
        public ClaimsPrincipal? Principal => null;
    }
}
