using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.FluentValidation.DependencyInjection.Tests;

public sealed class AddValidationBehaviorTests
{
    [Fact]
    public void AddValidationBehavior_NullOptions_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((MediatorOptions)null!).AddValidationBehavior());

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void AddValidationBehavior_ReturnsSameOptions()
    {
        var options = new MediatorOptions();

        Assert.Same(options, options.AddValidationBehavior());
    }

    [Fact]
    public void AddValidationBehavior_RegistersScopedBehaviorEvenWhenDefaultLifetimeIsSingleton()
    {
        var services = new ServiceCollection().AddMediator(o =>
        {
            o.DefaultLifetime = ServiceLifetime.Singleton;
            o.AddValidationBehavior();
        });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
        Assert.Equal(typeof(ValidationBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddValidationBehavior_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddValidationBehavior().AddValidationBehavior());

        Assert.Single(services, d => d.ServiceType == typeof(IBehavior));
    }

    [Fact]
    public void AddValidationBehavior_KeepsItsPositionAmongBehaviors()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddBehavior<OtherBehavior>().AddValidationBehavior());

        Assert.Equal(
            [typeof(OtherBehavior), typeof(ValidationBehavior)],
            services.Where(d => d.ServiceType == typeof(IBehavior)).Select(d => d.ImplementationType));
    }

    [Fact]
    public async Task EndToEnd_InvalidCommandThrows_ValidCommandReachesHandler()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddValidatorsFromAssemblyContaining<CreateUserValidator>()
            .AddMediator(o => o.AddHandler<CreateUserHandler>().AddValidationBehavior())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<Recorder>().Log;
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ValidationException>(async () => await mediator.SendAsync(new CreateUser(""), cancellationToken));
        await mediator.SendAsync(new CreateUser("Ana"), cancellationToken);

        Assert.Equal(["Ana"], log);
    }
}
