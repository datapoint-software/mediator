using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class PublishTests
{
    private readonly List<string> _log = [];

    [Fact]
    public async Task PublishAsync_NoHandlers_Completes()
    {
        var mediator = new ServiceProviderMediator(new FakeServiceProvider());

        await mediator.PublishAsync(new Notified(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_RunsHandlersInRegistrationOrder()
    {
        var provider = new FakeServiceProvider().AddAll<IMessageHandler<Notified>>(Logging("h1"), Logging("h2"), Logging("h3"));

        await new ServiceProviderMediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken);

        Assert.Equal(["h1", "h2", "h3"], _log);
    }

    [Fact]
    public async Task PublishAsync_PassesMessageAndTokenToEveryHandler()
    {
        using var cts = new CancellationTokenSource();
        var message = new Notified();
        var received = new List<(Notified Message, CancellationToken Token)>();
        var handler = new MessageHandler<Notified>((m, cancellationToken) =>
        {
            received.Add((m, cancellationToken));
            return default;
        });
        var provider = new FakeServiceProvider().AddAll<IMessageHandler<Notified>>(handler, handler);

        await new ServiceProviderMediator(provider).PublishAsync(message, cts.Token);

        Assert.Equal(2, received.Count);
        Assert.All(received, r =>
        {
            Assert.Same(message, r.Message);
            Assert.Equal(cts.Token, r.Token);
        });
    }

    [Fact]
    public async Task PublishAsync_HandlerThrowsAsynchronously_StopsAndPropagatesOriginalException()
    {
        var expected = new InvalidOperationException("h2 failed");
        var failing = new MessageHandler<Notified>(async (_, _) =>
        {
            await Task.Yield();
            _log.Add("h2");
            throw expected;
        });
        var provider = new FakeServiceProvider().AddAll(Logging("h1"), failing, Logging("h3"));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new ServiceProviderMediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(["h1", "h2"], _log);
    }

    [Fact]
    public async Task PublishAsync_HandlerThrowsSynchronously_StopsAndPropagatesOriginalException()
    {
        var expected = new InvalidOperationException("h2 failed");
        var failing = new MessageHandler<Notified>((_, _) => throw expected);
        var provider = new FakeServiceProvider().AddAll(Logging("h1"), failing, Logging("h3"));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new ServiceProviderMediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(["h1"], _log);
    }

    private IMessageHandler<Notified> Logging(string name) =>
        new MessageHandler<Notified>((_, _) =>
        {
            _log.Add(name);
            return default;
        });
}
