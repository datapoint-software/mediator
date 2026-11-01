using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthorizationObserverTests
{
    private readonly List<string> _log = [];
    private bool _nextCalled;

    [Fact]
    public async Task Allowed_IsObservedWithEveryDecision_BeforeTheHandlerRuns()
    {
        var observer = new RecordingObserver("observer", _log);
        var principal = Principals.SignedIn();
        var command = new DeleteInvoice(1);
        var behavior = CreateBehavior(principal, s => s
            .AddSingleton<IAuthorizationObserver>(observer)
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new TestAuthorizer<DeleteInvoice>("roles", AuthorizationDecision.Allow("finance"), _log))
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new TestAuthorizer<DeleteInvoice>("labels", AuthorizationDecision.Allow("unclassified"), _log)));

        await behavior.HandleAsync(command, Next, TestContext.Current.CancellationToken);

        Assert.Equal(["roles", "labels", "observer", "next"], _log);
        var authorization = Assert.Single(observer.Events);
        Assert.Same(command, authorization.Action);
        Assert.Same(principal, authorization.Principal);
        Assert.Equal(AuthorizationOutcome.Allowed, authorization.Outcome);
        Assert.Null(authorization.Exception);
        Assert.True(authorization.Result!.IsAllowed);
        Assert.Equal(["finance", "unclassified"], authorization.Result.Decisions.Select(d => (string)d.Decision.Details!));
        Assert.All(authorization.Result.Decisions, d => Assert.Equal(typeof(TestAuthorizer<DeleteInvoice>), d.AuthorizerType));
    }

    [Fact]
    public async Task Denied_IsObservedWithDecisionsUpToTheDenial()
    {
        var observer = new RecordingObserver("observer", _log);
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddSingleton<IAuthorizationObserver>(observer)
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new TestAuthorizer<DeleteInvoice>("roles", AuthorizationDecision.Allow("finance"), _log))
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new TestAuthorizer<DeleteInvoice>("labels", AuthorizationDecision.Deny("secret"), _log))
            .AddAuthorizer<DeleteInvoice>("never", true, _log));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["roles", "labels", "observer"], _log);
        var authorization = Assert.Single(observer.Events);
        Assert.Equal(AuthorizationOutcome.Denied, authorization.Outcome);
        Assert.Equal(["finance", "secret"], authorization.Result!.Decisions.Select(d => (string)d.Decision.Details!));
        Assert.False(authorization.Result.IsAllowed);
    }

    [Fact]
    public async Task NoAuthorizer_IsObservedAsDeniedWithoutDecisions()
    {
        var observer = new RecordingObserver("observer", _log);
        var behavior = CreateBehavior(Principals.SignedIn(), s => s.AddSingleton<IAuthorizationObserver>(observer));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new GetInvoice(), NextWithResult("x"), TestContext.Current.CancellationToken));

        var authorization = Assert.Single(observer.Events);
        Assert.Equal(AuthorizationOutcome.Denied, authorization.Outcome);
        Assert.Empty(authorization.Result!.Decisions);
    }

    [Fact]
    public async Task NoUser_IsObservedAsAuthenticationRequired()
    {
        var observer = new RecordingObserver("observer", _log);
        var behavior = CreateBehavior(principal: null, s => s
            .AddSingleton<IAuthorizationObserver>(observer)
            .AddAuthorizer<DeleteInvoice>("never", true, _log));

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["observer"], _log);
        var authorization = Assert.Single(observer.Events);
        Assert.Equal(AuthorizationOutcome.AuthenticationRequired, authorization.Outcome);
        Assert.Null(authorization.Principal);
        Assert.Null(authorization.Result);
    }

    [Fact]
    public async Task AuthorizerThrows_IsObservedAsFailed_AndItsExceptionPropagates()
    {
        var thrown = new InvalidOperationException("database down");
        var observer = new RecordingObserver("observer", _log);
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddSingleton<IAuthorizationObserver>(observer)
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new ThrowingAuthorizer<DeleteInvoice>(thrown)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
        Assert.False(_nextCalled);
        var authorization = Assert.Single(observer.Events);
        Assert.Equal(AuthorizationOutcome.Failed, authorization.Outcome);
        Assert.Same(thrown, authorization.Exception);
        Assert.Null(authorization.Result);
    }

    [Fact]
    public async Task AuthorizerThrows_WithoutObservers_ItsExceptionPropagates()
    {
        var thrown = new InvalidOperationException();
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new ThrowingAuthorizer<DeleteInvoice>(thrown)));

        Assert.Same(thrown, await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken)));
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task ObserverThrows_WhenAllowed_TheActionDoesNotRun()
    {
        var thrown = new InvalidOperationException("audit store down");
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddSingleton<IAuthorizationObserver>(new RecordingObserver("first", _log, thrown))
            .AddSingleton<IAuthorizationObserver>(new RecordingObserver("second", _log))
            .AddAuthorizer<DeleteInvoice>("allow", true, _log));

        Assert.Same(thrown, await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken)));

        Assert.False(_nextCalled);
        Assert.Equal(["allow", "first"], _log);
    }

    [Fact]
    public async Task SeveralObservers_RunInOrder()
    {
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddSingleton<IAuthorizationObserver>(new RecordingObserver("first", _log))
            .AddSingleton<IAuthorizationObserver>(new RecordingObserver("second", _log))
            .AddAuthorizer<GetInvoice>("allow", true, _log));

        await behavior.HandleAsync(new GetInvoice(), NextWithResult("x"), TestContext.Current.CancellationToken);

        Assert.Equal(["allow", "first", "second", "next"], _log);
    }

    [Fact]
    public async Task NonArrayObservers_AreUsed()
    {
        var observers = new List<IAuthorizationObserver> { new RecordingObserver("observer", _log) };
        var behavior = new AuthorizationBehavior(new DelegateServiceProvider(type =>
            type == typeof(IPrincipalAccessor) ? new FixedPrincipalAccessor(Principals.SignedIn())
            : type == typeof(IEnumerable<IAuthorizationObserver>) ? observers
            : null));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["observer"], _log);
    }

    [Fact]
    public async Task AnonymousAction_IsNotObserved()
    {
        var observer = new RecordingObserver("observer", _log);
        var behavior = CreateBehavior(principal: null, s => s.AddSingleton<IAuthorizationObserver>(observer));

        await behavior.HandleAsync(new GetPrices(), NextWithResult(1), TestContext.Current.CancellationToken);

        Assert.Empty(observer.Events);
    }

    private static AuthorizationBehavior CreateBehavior(ClaimsPrincipal? principal, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection().AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal));
        configure(services);
        return new AuthorizationBehavior(services.BuildServiceProvider());
    }

    private ValueTask Next(CancellationToken ct)
    {
        _nextCalled = true;
        _log.Add("next");
        return default;
    }

    private Func<CancellationToken, ValueTask<TResult>> NextWithResult<TResult>(TResult result) => _ =>
    {
        _nextCalled = true;
        _log.Add("next");
        return new(result);
    };
}
