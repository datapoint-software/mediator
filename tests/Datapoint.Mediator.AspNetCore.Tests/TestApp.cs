using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Datapoint.Mediator.AspNetCore.Tests;

// A real WebApplication, served in memory.
public sealed class TestApp : IAsyncDisposable
{
    private TestApp(WebApplication app)
    {
        App = app;
        Client = app.GetTestClient();
    }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<TestApp> StartAsync(
        Action<MediatorEndpointOptions>? configureEndpoints = null,
        Action<MediatorOptions>? configureMediator = null,
        Action<IServiceCollection>? configureServices = null,
        Action<RouteGroupBuilder>? configureGroup = null,
        Action<WebApplication>? configureApp = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<Recorder>();
        configureServices?.Invoke(builder.Services);
        builder.Services.AddMediator(o =>
        {
            o.AddHandler<CreateInvoiceHandler>()
                .AddHandler<ArchiveInvoiceHandler>()
                .AddHandler<GetInvoiceHandler>()
                .AddHandler<ListInvoicesHandler>()
                .AddHandler<FailingReportHandler>();
            configureMediator?.Invoke(o);
        });

        var app = builder.Build();
        configureApp?.Invoke(app);
        var group = app.MapActions("/api", configureEndpoints);
        configureGroup?.Invoke(group);
        await app.StartAsync();
        return new TestApp(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}
