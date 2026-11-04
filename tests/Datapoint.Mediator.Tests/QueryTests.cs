using System;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class QueryTests
{
    [Fact]
    public async Task QueryAsync_ReturnsHandlerResult()
    {
        var provider = new FakeServiceProvider()
            .Add<IQueryHandler<Echo, string>>(new QueryHandler<Echo, string>((q, _) => new(q.Value)));

        var result = await new ServiceProviderMediator(provider).QueryAsync(new Echo("hello"), TestContext.Current.CancellationToken);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task QueryAsync_PassesQueryAndTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var query = new Echo("value");
        Echo? receivedQuery = null;
        CancellationToken receivedToken = default;
        var provider = new FakeServiceProvider().Add<IQueryHandler<Echo, string>>(new QueryHandler<Echo, string>((q, cancellationToken) =>
        {
            receivedQuery = q;
            receivedToken = cancellationToken;
            return new(q.Value);
        }));

        await new ServiceProviderMediator(provider).QueryAsync(query, cts.Token);

        Assert.Same(query, receivedQuery);
        Assert.Equal(cts.Token, receivedToken);
    }

    [Fact]
    public async Task QueryAsync_MissingHandler_ThrowsNamingHandlerInterface()
    {
        var mediator = new ServiceProviderMediator(new FakeServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.QueryAsync(new Echo("value"), TestContext.Current.CancellationToken));

        Assert.Contains(typeof(IQueryHandler<Echo, string>).ToString(), exception.Message);
    }

    [Fact]
    public async Task QueryAsync_QueryWithTwoResultTypes_ReachesHandlerForRequestedResult()
    {
        var provider = new FakeServiceProvider()
            .Add<IQueryHandler<DualQuery, int>>(new QueryHandler<DualQuery, int>((_, _) => new(1)))
            .Add<IQueryHandler<DualQuery, string>>(new QueryHandler<DualQuery, string>((_, _) => new("one")));
        var mediator = new ServiceProviderMediator(provider);
        var query = new DualQuery();

        var number = await mediator.QueryAsync<int>(query, TestContext.Current.CancellationToken);
        var text = await mediator.QueryAsync<string>(query, TestContext.Current.CancellationToken);

        Assert.Equal(1, number);
        Assert.Equal("one", text);
    }
}
