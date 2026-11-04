using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class StreamQueryTests
{
    private readonly List<string> _log = [];

    [Fact]
    public async Task StreamAsync_ReturnsHandlerResultsInOrder()
    {
        var mediator = new ServiceProviderMediator(WithCountHandler(new FakeServiceProvider()));

        var results = await mediator.StreamAsync(new CountTo(3), TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], results);
    }

    [Fact]
    public async Task StreamAsync_PassesQueryAndTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var query = new CountTo(1);
        CountTo? receivedQuery = null;
        CancellationToken receivedToken = default;
        var provider = new FakeServiceProvider().Add<IStreamQueryHandler<CountTo, int>>(
            new StreamQueryHandler<CountTo, int>((q, cancellationToken) =>
            {
                receivedQuery = q;
                receivedToken = cancellationToken;
                return Streams.CountTo(q.Count, cancellationToken: cancellationToken);
            }));

        await new ServiceProviderMediator(provider).StreamAsync(query, cts.Token).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Same(query, receivedQuery);
        Assert.Equal(cts.Token, receivedToken);
    }

    [Fact]
    public void StreamAsync_NullQuery_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ServiceProviderMediator(new FakeServiceProvider()).StreamAsync((IStreamQuery<int>)null!, TestContext.Current.CancellationToken));

        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public void StreamAsync_MissingHandler_ThrowsBeforeEnumeration()
    {
        var mediator = new ServiceProviderMediator(new FakeServiceProvider());

        var exception = Assert.Throws<InvalidOperationException>(
            () => mediator.StreamAsync(new CountTo(1), TestContext.Current.CancellationToken));

        Assert.Contains(typeof(IStreamQueryHandler<CountTo, int>).ToString(), exception.Message);
    }

    [Fact]
    public async Task StreamAsync_IsLazy_HandlerRunsOnlyWhenEnumerated()
    {
        var mediator = new ServiceProviderMediator(WithCountHandler(new FakeServiceProvider()));

        var results = mediator.StreamAsync(new CountTo(2), TestContext.Current.CancellationToken);
        Assert.Empty(_log);

        await results.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["1", "2"], _log);
    }

    [Fact]
    public async Task StreamAsync_TokenPassedToStreamAsync_CancelsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var mediator = new ServiceProviderMediator(WithCountHandler(new FakeServiceProvider()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var i in mediator.StreamAsync(new CountTo(10), cts.Token))
            {
                if (i == 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.Equal(["1", "2"], _log);
    }

    [Fact]
    public async Task StreamAsync_TokenPassedToWithCancellation_CancelsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var mediator = new ServiceProviderMediator(WithCountHandler(new FakeServiceProvider()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var i in mediator.StreamAsync(new CountTo(10), TestContext.Current.CancellationToken).WithCancellation(cts.Token))
            {
                if (i == 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.Equal(["1", "2"], _log);
    }

    [Fact]
    public async Task StreamAsync_RunsBehaviorsInRegistrationOrder()
    {
        var provider = WithCountHandler(new FakeServiceProvider()
            .AddAll<IBehavior>(new LoggingBehavior("outer", _log), new LoggingBehavior("inner", _log)));

        await new ServiceProviderMediator(provider).StreamAsync(new CountTo(2), TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "1", "2", "<inner", "<outer"], _log);
    }

    [Fact]
    public async Task StreamAsync_BehaviorShortCircuits_HandlerIsNotCalled()
    {
        var provider = WithCountHandler(new FakeServiceProvider().AddAll<IBehavior>(new CachedStreamBehavior()));

        var results = await new ServiceProviderMediator(provider).StreamAsync(new CountTo(3), TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([42], results);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task StreamAsync_BehaviorOverridingOnlyCommands_DoesNotInterceptStreams()
    {
        var provider = WithCountHandler(new FakeServiceProvider().AddAll<IBehavior>(new CommandOnlyBehavior(_log)));

        var results = await new ServiceProviderMediator(provider).StreamAsync(new CountTo(1), TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1], results);
        Assert.Equal(["1"], _log);
    }

    private FakeServiceProvider WithCountHandler(FakeServiceProvider provider) =>
        provider.Add<IStreamQueryHandler<CountTo, int>>(
            new StreamQueryHandler<CountTo, int>((q, cancellationToken) => Streams.CountTo(q.Count, _log, cancellationToken)));

    private sealed class CachedStreamBehavior : IBehavior
    {
        public async IAsyncEnumerable<TResult> HandleAsync<TResult>(
            IStreamQuery<TResult> query,
            Func<CancellationToken, IAsyncEnumerable<TResult>> next,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return (TResult)(object)42;
        }
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
