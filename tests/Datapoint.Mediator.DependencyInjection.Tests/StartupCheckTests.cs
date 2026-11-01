using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.DependencyInjection.Tests;

public sealed class StartupCheckTests
{
    [Fact]
    public void Check_SeesCommandsQueriesAndStreamQueries_ButNotMessages()
    {
        IReadOnlyCollection<Type>? seen = null;

        new ServiceCollection().AddMediator(o => o
            .AddHandler<PingHandler>()
            .AddHandler<EchoHandler>()
            .AddHandler<CountToHandler>()
            .AddHandler<FirstNotifiedHandler>()
            .AddStartupCheck(c => seen = c.RequestTypes));

        Assert.Equal([typeof(Ping), typeof(Echo), typeof(CountTo)], seen);
    }

    [Fact]
    public void Check_ThatThrows_FailsAddMediator()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMediator(o => o
            .AddHandler<PingHandler>()
            .AddStartupCheck(_ => throw new InvalidOperationException("misconfigured"))));

        Assert.Equal("misconfigured", exception.Message);
    }

    [Fact]
    public void Check_RunsAgainForRequestsAddedByLaterCalls()
    {
        var seen = new List<IReadOnlyCollection<Type>>();
        var services = new ServiceCollection().AddMediator(o => o.AddHandler<PingHandler>().AddStartupCheck(c => seen.Add(c.RequestTypes)));

        services.AddMediator(o => o.AddHandler<EchoHandler>());

        Assert.Equal(2, seen.Count);
        Assert.Equal([typeof(Ping), typeof(Echo)], seen[1]);
    }

    [Fact]
    public void AddStartupCheck_Null_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddStartupCheck(null!));

        Assert.Equal("check", exception.ParamName);
    }
}
