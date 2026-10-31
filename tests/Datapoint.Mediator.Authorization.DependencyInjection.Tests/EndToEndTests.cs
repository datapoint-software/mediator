using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.DependencyInjection.Tests;

public sealed class EndToEndTests
{
    [Fact]
    public async Task AllowedCommand_ReachesHandler()
    {
        await using var provider = BuildProvider(FinanceUser());
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().SendAsync(new ApproveInvoice(500), TestContext.Current.CancellationToken);

        Assert.Equal(["approved 500"], provider.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task RefusedCommand_ThrowsAccessDenied()
    {
        await using var provider = BuildProvider(FinanceUser());
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // AlphaApproveAuthorizer refuses amounts above 1000.
        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await mediator.SendAsync(new ApproveInvoice(5000), TestContext.Current.CancellationToken));

        Assert.Empty(provider.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task NoUser_ThrowsAuthenticationRequired()
    {
        await using var provider = BuildProvider(principal: null);
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await mediator.SendAsync(new ApproveInvoice(500), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchByName_AllowedAction_ReachesHandler()
    {
        await using var provider = BuildProvider(FinanceUser());
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();

        await dispatcher.DispatchAsync("invoice.approve", JsonDocument.Parse("{ \"amount\": 500 }").RootElement, ct: TestContext.Current.CancellationToken);

        Assert.Equal(["approved 500"], provider.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task DispatchByName_ActionWithoutAuthorizer_IsDenied()
    {
        await using var provider = BuildProvider(FinanceUser());
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await dispatcher.DispatchAsync("invoice.void", default, ct: TestContext.Current.CancellationToken));

        Assert.Empty(provider.GetRequiredService<Recorder>().Log);
    }

    private static ServiceProvider BuildProvider(ClaimsPrincipal? principal) =>
        new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddScoped<IPrincipalAccessor>(_ => new FixedPrincipalAccessor(principal))
            .AddAuthorizersFromAssemblyContaining<EndToEndTests>()
            .AddMediator(o => o.AddHandler<ApproveInvoiceHandler>().AddHandler<VoidInvoiceHandler>().AddAuthorizationBehavior())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static ClaimsPrincipal FinanceUser() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana"), new Claim(ClaimTypes.Role, "Finance")], "test"));
}
