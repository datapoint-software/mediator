using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public async Task DispatchesThroughRealContainer()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<Recorder>().Log;
        var cancellationToken = TestContext.Current.CancellationToken;

        await mediator.SendAsync(new Ping(), cancellationToken);
        var sum = await mediator.SendAsync(new Add(2, 3), cancellationToken);
        var echo = await mediator.QueryAsync(new Echo("hello"), cancellationToken);
        await mediator.PublishAsync(new Notified(), cancellationToken);
        var counted = new List<int>();
        await foreach (var i in mediator.StreamAsync(new CountTo(3), cancellationToken))
        {
            counted.Add(i);
        }

        Assert.Equal(5, sum);
        Assert.Equal("hello", echo);
        Assert.Equal([1, 2, 3], counted);
        Assert.Equal(["outer>", "inner>", "ping", "<inner", "<outer", "first", "second"], log);
    }

    [Fact]
    public async Task ScopedHandler_IsSharedWithinScopeAndNewPerScope()
    {
        await using var provider = BuildProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        var a = first.ServiceProvider.GetRequiredService<ICommandHandler<Ping>>();
        var b = first.ServiceProvider.GetRequiredService<ICommandHandler<Ping>>();
        var c = second.ServiceProvider.GetRequiredService<ICommandHandler<Ping>>();

        Assert.Same(a, b);
        Assert.NotSame(a, c);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddMediator(o => o
                .AddHandler<PingHandler>()
                .AddHandler<AddHandler>()
                .AddHandler<EchoHandler>()
                .AddHandler<FirstNotifiedHandler>()
                .AddHandler<SecondNotifiedHandler>()
                .AddHandler<CountToHandler>()
                .AddBehavior<OuterBehavior>()
                .AddBehavior<InnerBehavior>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
