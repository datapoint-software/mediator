using System;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class MediatorTests
{
    private readonly Mediator _mediator = new(new FakeServiceProvider());

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new Mediator(null!));

        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Fact]
    public async Task SendAsync_NullCommand_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _mediator.SendAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact]
    public async Task SendAsyncWithResult_NullCommand_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _mediator.SendAsync((ICommand<int>)null!, TestContext.Current.CancellationToken));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact]
    public async Task QueryAsync_NullQuery_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _mediator.QueryAsync((IQuery<int>)null!, TestContext.Current.CancellationToken));

        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public async Task PublishAsync_NullMessage_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _mediator.PublishAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("message", exception.ParamName);
    }
}
