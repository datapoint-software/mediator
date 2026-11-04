using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class BehaviorTests
{
    private readonly List<string> _log = [];

    [Fact]
    public async Task SendAsync_RunsBehaviorsInRegistrationOrder()
    {
        var provider = WithOuterAndInner().Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, _) => Log("handler")));

        await new Mediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "handler", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task SendAsyncWithResult_RunsBehaviorsInRegistrationOrder()
    {
        var provider = WithOuterAndInner().Add<ICommandHandler<Add, int>>(new CommandHandler<Add, int>((c, _) =>
        {
            _log.Add("handler");
            return new(c.A + c.B);
        }));

        var result = await new Mediator(provider).SendAsync(new Add(1, 2), TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
        Assert.Equal(["outer>", "inner>", "handler", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task QueryAsync_RunsBehaviorsInRegistrationOrder()
    {
        var provider = WithOuterAndInner().Add<IQueryHandler<Echo, string>>(new QueryHandler<Echo, string>((q, _) =>
        {
            _log.Add("handler");
            return new(q.Value);
        }));

        var result = await new Mediator(provider).QueryAsync(new Echo("value"), TestContext.Current.CancellationToken);

        Assert.Equal("value", result);
        Assert.Equal(["outer>", "inner>", "handler", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task PublishAsync_RunsBehaviorsOnceAroundAllHandlers()
    {
        var provider = WithOuterAndInner().AddAll<IMessageHandler<Notified>>(
            new MessageHandler<Notified>((_, _) => Log("h1")),
            new MessageHandler<Notified>((_, _) => Log("h2")));

        await new Mediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "h1", "h2", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_StillRunsBehaviors()
    {
        var provider = WithOuterAndInner();

        await new Mediator(provider).PublishAsync(new Notified(), TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task QueryAsync_BehaviorShortCircuits_HandlerIsNotCalled()
    {
        var provider = new FakeServiceProvider()
            .AddAll<IBehavior>(new CachedQueryBehavior("cached"))
            .Add<IQueryHandler<Echo, string>>(new QueryHandler<Echo, string>((q, _) =>
            {
                _log.Add("handler");
                return new(q.Value);
            }));

        var result = await new Mediator(provider).QueryAsync(new Echo("value"), TestContext.Current.CancellationToken);

        Assert.Equal("cached", result);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task SendAsync_BehaviorCallsNextTwice_HandlerRunsTwice()
    {
        var calls = 0;
        var provider = new FakeServiceProvider()
            .AddAll<IBehavior>(new RetryOnceBehavior())
            .Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, _) =>
                ++calls == 1 ? throw new InvalidOperationException("first attempt") : default));

        await new Mediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SendAsync_BehaviorReplacesToken_HandlerReceivesReplacement()
    {
        using var replacement = new CancellationTokenSource();
        CancellationToken receivedToken = default;
        var provider = new FakeServiceProvider()
            .AddAll<IBehavior>(new ReplaceTokenBehavior(replacement.Token))
            .Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, cancellationToken) =>
            {
                receivedToken = cancellationToken;
                return default;
            }));

        await new Mediator(provider).SendAsync(new Ping(), TestContext.Current.CancellationToken);

        Assert.Equal(replacement.Token, receivedToken);
    }

    [Fact]
    public async Task Behavior_OverridingOnlyCommands_DoesNotInterceptQueriesOrMessages()
    {
        var provider = new FakeServiceProvider()
            .AddAll<IBehavior>(new CommandOnlyBehavior(_log))
            .Add<ICommandHandler<Ping>>(new CommandHandler<Ping>((_, _) => default))
            .Add<IQueryHandler<Echo, string>>(new QueryHandler<Echo, string>((q, _) => new(q.Value)))
            .AddAll<IMessageHandler<Notified>>(new MessageHandler<Notified>((_, _) => default));
        var mediator = new Mediator(provider);

        await mediator.QueryAsync(new Echo("value"), TestContext.Current.CancellationToken);
        await mediator.PublishAsync(new Notified(), TestContext.Current.CancellationToken);
        Assert.Empty(_log);

        await mediator.SendAsync(new Ping(), TestContext.Current.CancellationToken);
        Assert.Equal(["command"], _log);
    }

    private FakeServiceProvider WithOuterAndInner() =>
        new FakeServiceProvider().AddAll<IBehavior>(new LoggingBehavior("outer", _log), new LoggingBehavior("inner", _log));

    private ValueTask Log(string entry)
    {
        _log.Add(entry);
        return default;
    }

    private sealed class CachedQueryBehavior(string cached) : IBehavior
    {
        public ValueTask<TResult> HandleAsync<TResult>(
            IQuery<TResult> query,
            Func<CancellationToken, ValueTask<TResult>> next,
            CancellationToken cancellationToken = default) =>
            new((TResult)(object)cached);
    }

    private sealed class RetryOnceBehavior : IBehavior
    {
        public async ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
        {
            try
            {
                await next(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                await next(cancellationToken);
            }
        }
    }

    private sealed class ReplaceTokenBehavior(CancellationToken replacement) : IBehavior
    {
        public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default) =>
            next(replacement);
    }

    private sealed class CommandOnlyBehavior(List<string> log) : IBehavior
    {
        public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
        {
            log.Add("command");
            return next(cancellationToken);
        }
    }
}
