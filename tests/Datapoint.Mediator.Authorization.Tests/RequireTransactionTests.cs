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
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthorizationBehaviorOptions { RequireTransaction = (TransactionRequirement)9 });
        Assert.Equal(TransactionRequirement.None, new AuthorizationBehaviorOptions().RequireTransaction);
    }

    [Fact]
    public async Task Commands_WithoutATransaction_Throw_ButQueriesRun()
    {
        var behavior = Create(TransactionRequirement.Commands, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.HandleAsync(new DeleteInvoice(1), _ => default, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new CreateInvoice(), _ => new ValueTask<int>(1), Ct));
        Assert.Equal("x", await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
        Assert.Equal([1], await behavior.HandleAsync(new ListInvoices(), _ => new[] { 1 }.ToAsyncEnumerable(), Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task CommandsAndQueries_WithoutATransaction_Throw_StreamsAtFirstEnumeration()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>("x"), Ct));
        var results = behavior.HandleAsync(new ListInvoices(), _ => new[] { 1 }.ToAsyncEnumerable(), Ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await results.ToListAsync(Ct));

        Assert.Contains("inside a transaction", exception.Message);
        Assert.Empty(_log);   // refused before any authorizer runs
    }

    [Fact]
    public async Task InsideATransaction_EveryKindIsAuthorized()
    {
        var behavior = Create(TransactionRequirement.CommandsAndQueries, active: true);

        await behavior.HandleAsync(new DeleteInvoice(1), _ => default, Ct);
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

        await behavior.HandleAsync(new AnonymousBase(), _ => default, Ct);
    }

    [Fact]
    public async Task Options_ChangedAfterTheBehaviorIsCreated_HaveNoEffect()
    {
        var options = new AuthorizationBehaviorOptions();
        var behavior = new AuthorizationBehavior(Services(active: false), options);
        options.RequireTransaction = TransactionRequirement.CommandsAndQueries;

        await behavior.HandleAsync(new DeleteInvoice(1), _ => default, Ct);
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
