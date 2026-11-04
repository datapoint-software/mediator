using System;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class SendCommandWithResultTests
{
    [Fact]
    public async Task SendAsync_ReturnsHandlerResult()
    {
        var provider = new FakeServiceProvider()
            .Add<ICommandHandler<Add, int>>(new CommandHandler<Add, int>((c, _) => new(c.A + c.B)));

        var result = await new Mediator(provider).SendAsync(new Add(2, 3), TestContext.Current.CancellationToken);

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task SendAsync_PassesCommandAndTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var command = new Add(1, 1);
        Add? receivedCommand = null;
        CancellationToken receivedToken = default;
        var provider = new FakeServiceProvider().Add<ICommandHandler<Add, int>>(new CommandHandler<Add, int>((c, cancellationToken) =>
        {
            receivedCommand = c;
            receivedToken = cancellationToken;
            return new(0);
        }));

        await new Mediator(provider).SendAsync(command, cts.Token);

        Assert.Same(command, receivedCommand);
        Assert.Equal(cts.Token, receivedToken);
    }

    [Fact]
    public async Task SendAsync_MissingHandler_ThrowsNamingHandlerInterface()
    {
        var mediator = new Mediator(new FakeServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.SendAsync(new Add(1, 1), TestContext.Current.CancellationToken));

        Assert.Contains(typeof(ICommandHandler<Add, int>).ToString(), exception.Message);
    }

    [Fact]
    public async Task SendAsync_CommandWithTwoResultTypes_ReachesHandlerForRequestedResult()
    {
        var provider = new FakeServiceProvider()
            .Add<ICommandHandler<DualCommand, int>>(new CommandHandler<DualCommand, int>((_, _) => new(1)))
            .Add<ICommandHandler<DualCommand, string>>(new CommandHandler<DualCommand, string>((_, _) => new("one")));
        var mediator = new Mediator(provider);
        var command = new DualCommand();

        var number = await mediator.SendAsync<int>(command, TestContext.Current.CancellationToken);
        var text = await mediator.SendAsync<string>(command, TestContext.Current.CancellationToken);

        Assert.Equal(1, number);
        Assert.Equal("one", text);
    }
}
