using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class RequireTransactionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly List<string> _log = [];

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(
            () => new AuthorizationBehavior(new ServiceCollection().BuildServiceProvider(), null!)).ParamName);
    }

    [Fact]
    public void Options_RejectUndefinedValues()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AuthorizationBehaviorOptions { RequireTransaction = (TransactionRequirement)9 });

        Assert.StartsWith("AuthorizationBehaviorOptions.RequireTransaction must be one of None, ", exception.Message, StringComparison.Ordinal);
        Assert.Equal(TransactionRequirement.None, new AuthorizationBehaviorOptions().RequireTransaction);
    }

    [Fact]
    public async Task Commands_WithoutATransaction_Throw()
    {
        var behavior = Create(TransactionRequirement.Commands, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.HandleAsync(new DeleteInvoice(1), _ => default, Ct));
    }

    [Fact]
    public async Task Commands_WithoutATransaction_CommandsWithAResultThrow()
    {
        var behavior = Create(TransactionRequirement.Commands, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new CreateInvoice(), _ => new ValueTask<int>(1), Ct));
    }

    [Fact]
    public async Task Commands_WithoutATransaction_QueriesRun()
    {
        var behavior = Create(TransactionRequirement.Commands, active: false);

        Assert.Equal("x", await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
    }

    [Fact]
    public async Task Commands_WithoutATransaction_StreamQueriesRun()
    {
        var behavior = Create(TransactionRequirement.Commands, active: false);

        Assert.Equal([1], await behavior.HandleAsync(new ListInvoices(), _ => new[] { 1 }.ToAsyncEnumerable(), Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task CommandsAndQueries_WithoutATransaction_QueriesThrow()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));

        Assert.Contains("inside a transaction", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task CommandsAndQueries_WithoutATransaction_StreamQueriesThrowAtFirstEnumeration()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: false);

        var results = behavior.HandleAsync(new ListInvoices(), _ => new[] { 1 }.ToAsyncEnumerable(), Ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await results.ToListAsync(Ct));

        Assert.Contains("inside a transaction", exception.Message, StringComparison.Ordinal);

        // Refused before any authorizer runs.
        Assert.Empty(_log);
    }

    [Fact]
    public async Task InsideATransaction_EveryKindIsAuthorized()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: true);
        var ran = false;

        await behavior.HandleAsync(new DeleteInvoice(1), _ =>
        {
            ran = true;
            return default;
        }, Ct);
        Assert.True(ran);
        Assert.Equal("x", await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
        Assert.Equal([1], await behavior.HandleAsync(new ListInvoices(), _ => new[] { 1 }.ToAsyncEnumerable(), Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task NoTransactionContextRegistered_Throws()
    {
        var behavior = new AuthorizationBehavior(
            new ServiceCollection().AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(Principals.SignedIn())).BuildServiceProvider(),
            new AuthorizationBehaviorOptions { RequireTransaction = TransactionRequirement.Commands });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.HandleAsync(new DeleteInvoice(1), _ => default, Ct));
    }

    [Fact]
    public async Task AnonymousActions_AreNotChecked()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: false);
        var ran = false;

        await behavior.HandleAsync(new AnonymousBase(), _ =>
        {
            ran = true;
            return default;
        }, Ct);

        Assert.True(ran);
    }

    [Fact]
    public async Task Options_ChangedAfterTheBehaviorIsCreated_HaveNoEffect()
    {
        var options = new AuthorizationBehaviorOptions();
        var behavior = new AuthorizationBehavior(Services(active: false), options);
        options.RequireTransaction = TransactionRequirement.CommandsAndQueries;
        var ran = false;

        await behavior.HandleAsync(new DeleteInvoice(1), _ =>
        {
            ran = true;
            return default;
        }, Ct);

        Assert.True(ran);
    }

    private AuthorizationBehavior Create(TransactionRequirement requirement, bool active) =>
        new(Services(active), new AuthorizationBehaviorOptions { RequireTransaction = requirement });

    private ServiceProvider Services(bool active) =>
        new ServiceCollection()
            .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(Principals.SignedIn()))
            .AddSingleton<ITransactionContext>(new FakeTransactionContext(active))
            .AddAuthorizer<DeleteInvoice>("delete", true, _log)
            .AddAuthorizer<CreateInvoice>("create", true, _log)
            .AddAuthorizer<GetInvoice>("get", true, _log)
            .AddAuthorizer<ListInvoices>("list", true, _log)
            .BuildServiceProvider();
}
