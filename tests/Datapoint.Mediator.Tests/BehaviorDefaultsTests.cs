using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

// The default IBehavior methods must pass straight through to next, with the same token.
public sealed class BehaviorDefaultsTests
{
    private readonly IBehavior _behavior = new PassThroughBehavior();

    [Fact]
    public async Task Command_CallsNextWithSameToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;

        await _behavior.HandleAsync(new Ping(), cancellationToken =>
        {
            received = cancellationToken;
            return default;
        }, cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task CommandWithResult_CallsNextWithSameTokenAndReturnsItsResult()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;

        var result = await _behavior.HandleAsync(new Add(1, 2), cancellationToken =>
        {
            received = cancellationToken;
            return new ValueTask<int>(42);
        }, cts.Token);

        Assert.Equal(42, result);
        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task Query_CallsNextWithSameTokenAndReturnsItsResult()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;

        var result = await _behavior.HandleAsync(new Echo("value"), cancellationToken =>
        {
            received = cancellationToken;
            return new ValueTask<string>("result");
        }, cts.Token);

        Assert.Equal("result", result);
        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task Message_CallsNextWithSameToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;

        await _behavior.HandleAsync(new Notified(), cancellationToken =>
        {
            received = cancellationToken;
            return default;
        }, cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public void StreamQuery_CallsNextWithSameTokenAndReturnsItsResult()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;
        var expected = Streams.CountTo(1, cancellationToken: TestContext.Current.CancellationToken);

        var result = _behavior.HandleAsync(new CountTo(1), cancellationToken =>
        {
            received = cancellationToken;
            return expected;
        }, cts.Token);

        Assert.Same(expected, result);
        Assert.Equal(cts.Token, received);
    }

    private sealed class PassThroughBehavior : IBehavior;
}
