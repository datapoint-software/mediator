using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.CommandLine.Tests;

// A CLI built from the catalog, with in-memory output.
public sealed class Cli : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public Cli(
        Action<MediatorCommandLineOptions>? configureCommands = null,
        Action<MediatorOptions>? configureMediator = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection().AddSingleton<Recorder>().AddScoped<ScopedMarker>();
        configureServices?.Invoke(services);
        services.AddMediator(o =>
        {
            o.AddHandlersFromAssemblyContaining<Cli>();
            configureMediator?.Invoke(o);
        });
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Root = new RootCommand("Invoices").AddMediatorActions(_provider, configureCommands);
    }

    public RootCommand Root { get; }

    public Recorder Recorder => _provider.GetRequiredService<Recorder>();

    public async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await Root.Parse(args).InvokeAsync(
            new InvocationConfiguration { Output = output, Error = error },
            TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
