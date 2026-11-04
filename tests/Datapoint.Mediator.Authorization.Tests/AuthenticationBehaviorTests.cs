using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthenticationBehaviorTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly List<string> _log = [];
    private bool _nextCalled;

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        Assert.Equal("serviceProvider", Assert.Throws<ArgumentNullException>(() => new AuthenticationBehavior(null!)).ParamName);
    }

    [Fact]
    public void HandleAsync_NullArguments_Throw()
    {
        var behavior = new AuthenticationBehavior(new ServiceCollection().BuildServiceProvider());

        BehaviorArguments.AssertNullsAreRefused(behavior, new DeleteInvoice(1), new CreateInvoice(), new GetInvoice(), new ListInvoices());
    }

    [Fact]
    public async Task SignedIn_EveryKindRuns_WithoutAnyAuthorizer()
    {
        var behavior = Create(Principals.SignedIn(), out _);

        await behavior.HandleAsync(new DeleteInvoice(1), Next, Ct);
        Assert.Equal(5, await behavior.HandleAsync(new CreateInvoice(), _ => new ValueTask<int>(5), Ct));
        Assert.Equal("x", await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
        Assert.Equal([1, 2], await behavior.HandleAsync(new ListInvoices(), Stream, Ct).ToListAsync(Ct));

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task NotSignedIn_EveryKindIsRefused_AndObservedOnce()
    {
        var behavior = Create(Principals.Anonymous(), out var observer);

        await Assert.ThrowsAsync<AuthenticationRequiredException>(async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, Ct));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new CreateInvoice(), _ => new ValueTask<int>(5), Ct));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
        var results = behavior.HandleAsync(new ListInvoices(), Stream, Ct);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(async () => await results.ToListAsync(Ct));

        Assert.False(_nextCalled);
        Assert.Equal(4, observer.Events.Count);
        Assert.All(observer.Events, e => Assert.Equal(AuthorizationOutcome.AuthenticationRequired, e.Outcome));
    }

    [Fact]
    public async Task AnonymousActions_RunWithoutAUser()
    {
        var behavior = new AuthenticationBehavior(new ServiceCollection().BuildServiceProvider());

        await behavior.HandleAsync(new AnonymousBase(), Next, Ct);
        Assert.Equal(3, await behavior.HandleAsync(new GetPrices(), _ => new ValueTask<int>(3), Ct));
        Assert.Equal([1, 2], await behavior.HandleAsync(new ListPrices(), Stream, Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task AnonymousCommandWithResult_RunsWithoutAUser()
    {
        var behavior = new AuthenticationBehavior(new ServiceCollection().BuildServiceProvider());

        Assert.Equal(7, await behavior.HandleAsync(new AnonymousWithResult(), _ => new ValueTask<int>(7), Ct));
    }

    private AuthenticationBehavior Create(ClaimsPrincipal principal, out RecordingObserver observer)
    {
        observer = new RecordingObserver("observer", _log);
        return new AuthenticationBehavior(new ServiceCollection()
            .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal))
            .AddSingleton<IAuthorizationObserver>(observer)
            .BuildServiceProvider());
    }

    private ValueTask Next(CancellationToken cancellationToken)
    {
        _nextCalled = true;
        return default;
    }

    private async IAsyncEnumerable<int> Stream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _nextCalled = true;
        await Task.Yield();
        yield return 1;
        yield return 2;
    }
}
