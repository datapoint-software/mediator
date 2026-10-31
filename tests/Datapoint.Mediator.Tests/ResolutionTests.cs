using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

// Containers other than Microsoft's may return null or a non-array enumerable for IEnumerable<T>.
public sealed class ResolutionTests
{
    private readonly List<string> _log = [];

    [Fact]
    public async Task NullBehaviorsAndMessageHandlers_AreTreatedAsEmpty()
    {
        var provider = new FakeServiceProvider()
            .Set(typeof(IEnumerable<IBehavior>), null)
            .Set(typeof(IEnumerable<IMessageHandler<Notified>>), null);

        await new Mediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NonArrayBehaviors_RunInOrder()
    {
        var behaviors = new List<IBehavior> { new LoggingBehavior("outer", _log), new LoggingBehavior("inner", _log) };
        var provider = new FakeServiceProvider()
            .Set(typeof(IEnumerable<IBehavior>), behaviors)
            .Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, _) => default));

        await new Mediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task NonArrayMessageHandlers_RunInOrder()
    {
        var handlers = new List<IMessageHandler<Notified>>
        {
            new MessageHandler<Notified>((_, _) => Log("h1")),
            new MessageHandler<Notified>((_, _) => Log("h2")),
        };
        var provider = new FakeServiceProvider().Set(typeof(IEnumerable<IMessageHandler<Notified>>), handlers);

        await new Mediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken);

        Assert.Equal(["h1", "h2"], _log);
    }

    [Fact]
    public async Task HandlerOfWrongType_IsTreatedAsMissing()
    {
        var provider = new FakeServiceProvider().Set(typeof(ICommandHandler<Ping>), new object());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new Mediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken));
    }

    private ValueTask Log(string entry)
    {
        _log.Add(entry);
        return default;
    }
}
