using System;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class SendCommandTests
{
    [Fact]
    public async Task SendAsync_PassesCommandAndTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var command = new Ping();
        Ping? receivedCommand = null;
        CancellationToken receivedToken = default;
        var provider = new FakeServiceProvider().Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((c, cancellationToken) =>
        {
            receivedCommand = c;
            receivedToken = cancellationToken;
            return default;
        }));

        await new ServiceProviderMediator(provider).SendAsync(command, cts.Token);

        Assert.Same(command, receivedCommand);
        Assert.Equal(cts.Token, receivedToken);
    }

    [Fact]
    public async Task SendAsync_MissingHandler_ThrowsNamingHandlerInterface()
    {
        var mediator = new ServiceProviderMediator(new FakeServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.SendAsync(new Ping(), TestContext.Current.CancellationToken));

        Assert.Contains(typeof(ICommandHandler<Ping>).ToString(), exception.Message);
    }

    [Fact]
    public async Task SendAsync_HandlerThrows_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("handler failed");
        var provider = new FakeServiceProvider()
            .Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, _) => throw expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new ServiceProviderMediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }
}
