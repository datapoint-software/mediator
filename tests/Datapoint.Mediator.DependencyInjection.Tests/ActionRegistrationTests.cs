using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class ActionRegistrationTests
{
    [Fact]
    public void Catalog_ListsExposedNamedActionsWithHandlers()
    {
        using var provider = new ServiceCollection()
            .AddMediator(o => o.AddHandler<GenerateReportHandler>().AddHandler<SecretReportHandler>().AddHandler<PingHandler>())
            .BuildServiceProvider();

        var catalog = provider.GetRequiredService<IActionCatalog>();

        var action = Assert.Single(catalog.Actions);
        Assert.Equal("report.generate", action.Name);
        Assert.Equal("Generates a report.", action.Description);
        Assert.Equal(ActionKind.CommandWithResult, action.Kind);
        Assert.Equal(typeof(string), action.ResultType);
        Assert.False(catalog.TryGetAction("report.secret", out _));
    }

    [Fact]
    public void Services_HaveExpectedLifetimes()
    {
        var services = new ServiceCollection().AddMediator(_ => { });

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == typeof(IActionCatalog)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, d => d.ServiceType == typeof(IActionDispatcher)).Lifetime);
    }

    [Fact]
    public void AddMediatorCalledTwice_AddsActionsFromBothCalls()
    {
        var services = new ServiceCollection()
            .AddMediator(o => o.AddHandler<GenerateReportHandler>())
            .AddMediator(o => o.AddHandler<ArchiveReportHandler>());
        using var provider = services.BuildServiceProvider();

        Assert.Equal(["report.archive", "report.generate"], provider.GetRequiredService<IActionCatalog>().Actions.Select(a => a.Name));
        Assert.Single(services, d => d.ServiceType == typeof(IActionCatalog));
    }

    [Fact]
    public void DuplicateNameAcrossCalls_ThrowsAtRegistration()
    {
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<GenerateReportHandler>());

        Assert.Throws<InvalidOperationException>(() => services.AddMediator(o => o.AddHandler<DuplicateReportHandler>()));
    }

    [Fact]
    public void InvalidName_ThrowsAtRegistration()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o.AddHandler<BadlyNamedReportHandler>()));
    }

    [Fact]
    public void NamedMessage_ThrowsAtRegistration()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o.AddHandler<ReportCreatedHandler>()));
    }

    [Fact]
    public void CustomCatalog_RegisteredBefore_IsKept()
    {
        var custom = new ActionCatalog([]);
        var services = new ServiceCollection().AddSingleton<IActionCatalog>(custom).AddMediator(o => o.AddHandler<GenerateReportHandler>());
        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IActionCatalog>());
    }

    [Fact]
    public async Task Dispatch_ThroughRealContainer()
    {
        await using var provider = new ServiceCollection()
            .AddMediator(o => o.AddHandler<GenerateReportHandler>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            "report.generate",
            JsonDocument.Parse("""{ "title": "Q4" }""").RootElement,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal("report: Q4", result);
    }
}
