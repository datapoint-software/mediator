using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using global::ModelContextProtocol.Client;
using global::ModelContextProtocol.Protocol;
using global::ModelContextProtocol.Server;

namespace Datapoint.Mediator.ModelContextProtocol.Tests;

// A real MCP server and client, connected through in-memory pipes.
public sealed class McpHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly McpServer _server;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _running;

    private McpHarness(ServiceProvider provider, McpServer server, McpClient client)
    {
        _provider = provider;
        _server = server;
        Client = client;
        _running = server.RunAsync(_stop.Token);
    }

    public McpClient Client { get; private set; }

    public Recorder Recorder => _provider.GetRequiredService<Recorder>();

    public static async Task<McpHarness> StartAsync(
        Action<MediatorMcpOptions>? configureMcp = null,
        Action<MediatorOptions>? configureMediator = null,
        Action<IServiceCollection>? configureServices = null,
        bool withServiceProvider = true)
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        configureServices?.Invoke(services);
        services.AddMediator(o =>
        {
            o.AddHandler<CreateInvoiceHandler>()
                .AddHandler<ArchiveInvoiceHandler>()
                .AddHandler<GetInvoiceHandler>()
                .AddHandler<ListInvoicesHandler>()
                .AddHandler<FailingReportHandler>()
                .AddHandler<RejectedReportHandler>();
            configureMediator?.Invoke(o);
        });
        services.AddMcpServer().WithMediatorActions(configureMcp);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
            options,
            serviceProvider: withServiceProvider ? provider : null);

        var harness = new McpHarness(provider, server, null!);
        harness.Client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _stop.CancelAsync();
        await _server.DisposeAsync();
        try
        {
            await _running;
        }
        catch (OperationCanceledException)
        {
        }

        await _provider.DisposeAsync();
        _stop.Dispose();
    }
}
