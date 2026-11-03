using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Datapoint.Mediator.Authorization.AspNetCore.Tests;

public sealed class HttpPrincipalAccessorTests
{
    [Fact]
    public async Task AuthenticatedRequest_RunsAsTheRequestUser()
    {
        await using var app = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/invoices/approve");
        request.Headers.Add(HeaderAuthenticationHandler.HeaderName, "ana");

        using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["approved by ana"], app.Services.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public async Task AnonymousRequest_Returns401()
    {
        await using var app = await StartAsync();

        using var response = await app.GetTestClient().PostAsync("/api/invoices/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<Recorder>().Log);
    }

    [Fact]
    public void Principal_OutsideRequest_IsNull()
    {
        Assert.Null(new HttpPrincipalAccessor(new HttpContextAccessor()).Principal);
    }

    [Fact]
    public void Constructor_NullAccessor_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new HttpPrincipalAccessor(null!));

        Assert.Equal("httpContextAccessor", exception.ParamName);
    }

    [Fact]
    public void AddHttpPrincipalAccessor_RegistersScopedAccessorAndHttpContextAccessor()
    {
        var services = new ServiceCollection().AddHttpPrincipalAccessor();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPrincipalAccessor));
        Assert.Equal(typeof(HttpPrincipalAccessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Single(services, d => d.ServiceType == typeof(IHttpContextAccessor));
    }

    [Fact]
    public void AddHttpPrincipalAccessor_Twice_RegistersOnce()
    {
        var services = new ServiceCollection().AddHttpPrincipalAccessor().AddHttpPrincipalAccessor();

        Assert.Single(services, d => d.ServiceType == typeof(IPrincipalAccessor));
    }

    [Fact]
    public void AddHttpPrincipalAccessor_OtherAccessorRegistered_Throws()
    {
        var services = new ServiceCollection().AddFixedPrincipalAccessor(null);

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddHttpPrincipalAccessor());

        Assert.Contains("another IPrincipalAccessor is already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHttpPrincipalAccessor_IgnoresKeyedAccessors()
    {
        var services = new ServiceCollection().AddKeyedSingleton<IPrincipalAccessor>("other", new FixedPrincipalAccessor(null));

        services.AddHttpPrincipalAccessor();

        Assert.Single(services, d => d.ServiceType == typeof(IPrincipalAccessor) && !d.IsKeyedService);
    }

    [Fact]
    public void AddHttpPrincipalAccessor_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddHttpPrincipalAccessor());

        Assert.Equal("services", exception.ParamName);
    }

    // A real WebApplication, served in memory, with authentication, the authorization behaviour and MapMediatorActions.
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<Recorder>();
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddHttpPrincipalAccessor();
        builder.Services.AddAuthorizersFromAssemblyContaining<HttpPrincipalAccessorTests>();
        builder.Services.AddMediator(o => o.AddHandler<ApproveInvoiceHandler>().AddAuthorizationBehavior());

        var app = builder.Build();
        app.UseAuthentication();
        app.MapMediatorActions("/api");
        await app.StartAsync();
        return app;
    }
}

[Action("invoices.approve", "A test action.")]
public sealed record ApproveInvoice : ICommand;

public sealed class Recorder
{
    public List<string> Log { get; } = [];
}

public sealed class ApproveInvoiceHandler(Recorder recorder, IPrincipalAccessor principalAccessor) : ICommandHandler<ApproveInvoice>
{
    public ValueTask HandleAsync(ApproveInvoice command, CancellationToken ct = default)
    {
        recorder.Log.Add($"approved by {principalAccessor.Principal?.Identity?.Name}");
        return default;
    }
}

public sealed class ApproveInvoiceAuthorizer : IAuthorizer<ApproveInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(ApproveInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.Allow());
}

// Signs in the user named in a request header, so tests can choose the user without real credentials.
public sealed class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Header";
    public const string HeaderName = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var name))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
