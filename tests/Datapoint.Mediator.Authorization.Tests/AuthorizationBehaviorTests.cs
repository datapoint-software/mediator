using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthorizationBehaviorTests
{
    private readonly List<string> _log = [];
    private bool _nextCalled;

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new AuthorizationBehavior(null!));

        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Fact]
    public async Task AnonymousAction_IsAllowedWithoutUserOrAccessor()
    {
        var behavior = new AuthorizationBehavior(new ServiceCollection().BuildServiceProvider());

        await behavior.HandleAsync(new GetPrices(), NextWithResult(1), TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task NoPrincipalAccessor_Throws()
    {
        var behavior = new AuthorizationBehavior(new ServiceCollection().BuildServiceProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task NoPrincipal_ThrowsAuthenticationRequired()
    {
        var behavior = CreateBehavior(principal: null, s => s.AddAuthorizer<DeleteInvoice>("allow", true, _log));

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_ThrowsAuthenticationRequired()
    {
        var behavior = CreateBehavior(Principals.Anonymous(), s => s.AddAuthorizer<DeleteInvoice>("allow", true, _log));

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task PrincipalWithoutIdentity_ThrowsAuthenticationRequired()
    {
        var behavior = CreateBehavior(Principals.WithoutIdentity(), s => s.AddAuthorizer<DeleteInvoice>("allow", true, _log));

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task AnonymousCommand_IsAllowedWithoutUserOrAccessor()
    {
        var behavior = new AuthorizationBehavior(new ServiceCollection().BuildServiceProvider());

        await behavior.HandleAsync(new AnonymousBase(), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task AnonymousStreamQuery_IsAllowedWithoutUserOrAccessor()
    {
        var behavior = new AuthorizationBehavior(new ServiceCollection().BuildServiceProvider());

        var results = await behavior.HandleAsync(new ListPrices(), NextStream, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], results);
    }

    [Fact]
    public async Task NullAuthorizers_DenyAccess()
    {
        var behavior = new AuthorizationBehavior(new DelegateServiceProvider(type =>
            type == typeof(IPrincipalAccessor) ? new FixedPrincipalAccessor(Principals.SignedIn()) : null));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task NonArrayAuthorizers_AreUsed()
    {
        var authorizers = new List<IAuthorizer<DeleteInvoice>>
        {
            new TestAuthorizer<DeleteInvoice>("first", true, _log),
            new TestAuthorizer<DeleteInvoice>("second", false, _log),
        };
        var behavior = new AuthorizationBehavior(new DelegateServiceProvider(type =>
            type == typeof(IPrincipalAccessor) ? new FixedPrincipalAccessor(Principals.SignedIn())
            : type == typeof(IEnumerable<IAuthorizer<DeleteInvoice>>) ? authorizers
            : null));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["first", "second"], _log);
    }

    [Fact]
    public async Task NoAuthorizer_DeniesAccess()
    {
        var behavior = CreateBehavior(Principals.SignedIn(), _ => { });

        var exception = await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Contains("No authorizer", exception.Message);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task AuthorizerAllows_CallsNext()
    {
        var behavior = CreateBehavior(Principals.SignedIn(), s => s.AddAuthorizer<DeleteInvoice>("allow", true, _log));

        await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task AuthorizerRefuses_DeniesAccess()
    {
        var behavior = CreateBehavior(Principals.SignedIn(), s => s.AddAuthorizer<DeleteInvoice>("deny", false, _log));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task SeveralAuthorizers_AllMustAllow_AndStopAtFirstRefusal()
    {
        var behavior = CreateBehavior(Principals.SignedIn(), s => s
            .AddAuthorizer<DeleteInvoice>("first", true, _log)
            .AddAuthorizer<DeleteInvoice>("second", false, _log)
            .AddAuthorizer<DeleteInvoice>("third", true, _log));

        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await behavior.HandleAsync(new DeleteInvoice(1), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["first", "second"], _log);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task Authorizer_ReceivesActionPrincipalAndToken()
    {
        using var cts = new CancellationTokenSource();
        var principal = Principals.SignedIn();
        var authorizer = new TestAuthorizer<DeleteInvoice>("allow", true, _log);
        var behavior = CreateBehavior(principal, s => s.AddSingleton<IAuthorizer<DeleteInvoice>>(authorizer));
        var command = new DeleteInvoice(7);

        await behavior.HandleAsync(command, Next, cts.Token);

        var call = Assert.Single(authorizer.Calls);
        Assert.Same(command, call.Action);
        Assert.Same(principal, call.Principal);
        Assert.Equal(cts.Token, call.Token);
    }

    [Fact]
    public async Task CommandWithResult_IsAuthorized()
    {
        var allowed = CreateBehavior(Principals.SignedIn(), s => s.AddAuthorizer<CreateInvoice>("allow", true, _log));
        var denied = CreateBehavior(Principals.SignedIn(), _ => { });

        Assert.Equal(5, await allowed.HandleAsync(new CreateInvoice(), NextWithResult(5), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await denied.HandleAsync(new CreateInvoice(), NextWithResult(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Query_IsAuthorized()
    {
        var allowed = CreateBehavior(Principals.SignedIn(), s => s.AddAuthorizer<GetInvoice>("allow", true, _log));
        var denied = CreateBehavior(Principals.SignedIn(), _ => { });

        Assert.Equal("invoice", await allowed.HandleAsync(new GetInvoice(), NextWithResult("invoice"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await denied.HandleAsync(new GetInvoice(), NextWithResult("invoice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StreamQuery_IsAuthorizedWhenEnumerated()
    {
        var allowed = CreateBehavior(Principals.SignedIn(), s => s.AddAuthorizer<ListInvoices>("allow", true, _log));
        var denied = CreateBehavior(Principals.SignedIn(), _ => { });

        var results = await allowed.HandleAsync(new ListInvoices(), NextStream, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], results);

        _nextCalled = false;
        var deniedResults = denied.HandleAsync(new ListInvoices(), NextStream, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AccessDeniedException>(
            async () => await deniedResults.ToListAsync(TestContext.Current.CancellationToken));
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task Message_IsNotAuthorized()
    {
        // The message overload is a default interface method, so it is called through IBehavior, as the mediator does.
        IBehavior behavior = CreateBehavior(principal: null, _ => { });

        await behavior.HandleAsync(new InvoiceCreated(), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task AnonymousAttribute_IsNotInherited()
    {
        var behavior = CreateBehavior(principal: null, _ => { });

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.HandleAsync(new DerivedFromAnonymous(), Next, TestContext.Current.CancellationToken));
        await behavior.HandleAsync(new AnonymousBase(), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    private AuthorizationBehavior CreateBehavior(ClaimsPrincipal? principal, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection().AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(principal));
        configure(services);
        return new AuthorizationBehavior(services.BuildServiceProvider());
    }

    private ValueTask Next(CancellationToken ct)
    {
        _nextCalled = true;
        return default;
    }

    private Func<CancellationToken, ValueTask<TResult>> NextWithResult<TResult>(TResult result) => _ =>
    {
        _nextCalled = true;
        return new(result);
    };

    private async IAsyncEnumerable<int> NextStream([EnumeratorCancellation] CancellationToken ct)
    {
        _nextCalled = true;
        await Task.Yield();
        yield return 1;
        yield return 2;
    }
}

internal static class ServiceCollectionTestExtensions
{
    public static IServiceCollection AddAuthorizer<TAction>(this IServiceCollection services, string name, bool allow, List<string> log) =>
        services.AddSingleton<IAuthorizer<TAction>>(new TestAuthorizer<TAction>(name, allow, log));
}
