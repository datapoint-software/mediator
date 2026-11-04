using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthorizationContextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly List<string> _log = [];
    private readonly List<string?> _streamSaw = [];
    private readonly AuthorizationContext _context = new();
    private readonly AuthorizationBehavior _behavior;

    public AuthorizationContextTests()
    {
        _behavior = new AuthorizationBehavior(new ServiceCollection()
            .AddSingleton<IPrincipalAccessor>(new FixedPrincipalAccessor(Principals.SignedIn()))
            .AddSingleton<IAuthorizer<DeleteInvoice>>(new TestAuthorizer<DeleteInvoice>("delete", AuthorizationDecision.Allow("delete grant"), _log))
            .AddSingleton<IAuthorizer<CreateInvoice>>(new TestAuthorizer<CreateInvoice>("create", AuthorizationDecision.Allow("create grant"), _log))
            .AddSingleton<IAuthorizer<GetInvoice>>(new TestAuthorizer<GetInvoice>("get", AuthorizationDecision.Allow("get grant"), _log))
            .AddSingleton<IAuthorizer<ListInvoices>>(new TestAuthorizer<ListInvoices>("list", AuthorizationDecision.Allow("list grant"), _log))
            .BuildServiceProvider());
    }

    [Fact]
    public async Task Command_SeesItsDecisionWhileItRuns_AndNotAfterwards()
    {
        AuthorizationResult? seen = null;

        await _behavior.HandleAsync(new DeleteInvoice(1), _ =>
        {
            seen = _context.Current;
            return default;
        }, Ct);

        Assert.Equal(typeof(DeleteInvoice), seen!.ActionType);
        Assert.Equal("delete grant", Details(seen));
        Assert.Null(_context.Current);
    }

    [Fact]
    public async Task CommandWithResultAndQuery_SeeTheirDecisions()
    {
        string? created = null;

        await _behavior.HandleAsync(new CreateInvoice(), _ =>
        {
            created = Details(_context.Current);
            return new ValueTask<int>(1);
        }, Ct);
        var read = await _behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>(Details(_context.Current)!), Ct);

        Assert.Equal("create grant", created);
        Assert.Equal("get grant", read);
    }

    [Fact]
    public async Task NestedAction_SeesItsOwnDecision_ThenTheOuterOneAgain()
    {
        var seen = new List<string?>();

        await _behavior.HandleAsync(new DeleteInvoice(1), async cancellationToken =>
        {
            seen.Add(Details(_context.Current));
            seen.Add(await _behavior.HandleAsync(new GetInvoice(), _ => new ValueTask<string>(Details(_context.Current)!), cancellationToken));
            seen.Add(Details(_context.Current));
        }, Ct);

        Assert.Equal(["delete grant", "get grant", "delete grant"], seen);
    }

    [Fact]
    public async Task NestedAnonymousActions_SeeNoDecision_ThenTheOuterOneAgain()
    {
        var seen = new List<string?>();

        await _behavior.HandleAsync(new DeleteInvoice(1), async cancellationToken =>
        {
            await _behavior.HandleAsync(new AnonymousBase(), _ =>
            {
                seen.Add(Details(_context.Current));
                return default;
            }, cancellationToken);
            seen.Add(await _behavior.HandleAsync(new GetPrices(), _ => new ValueTask<int>(_context.Current is null ? 0 : 1), cancellationToken) == 0 ? null : "seen");
            await foreach (var _ in _behavior.HandleAsync(new ListPrices(), c => Stream(c), cancellationToken))
            {
            }

            seen.AddRange(_streamSaw);

            seen.Add(Details(_context.Current));
        }, Ct);

        Assert.Equal([null, null, null, null, "delete grant"], seen);
    }

    [Fact]
    public async Task StreamQuery_SeesItsDecisionForEveryResult_AndTheConsumerDoesNot()
    {
        var consumerSaw = new List<string?>();

        await foreach (var _ in _behavior.HandleAsync(new ListInvoices(), c => Stream(c), Ct))
        {
            await Task.Yield();
            consumerSaw.Add(Details(_context.Current));
        }

        Assert.Equal(["list grant", "list grant"], _streamSaw);
        Assert.Equal([null, null], consumerSaw);
    }

    [Fact]
    public async Task ParallelDispatches_SeeTheirOwnDecisions()
    {
        using var bothStarted = new Barrier(2);

        async ValueTask<string> ReadAfterOtherStarts(CancellationToken cancellationToken)
        {
            await Task.Yield();
            bothStarted.SignalAndWait(cancellationToken);
            return Details(_context.Current)!;
        }

        var get = Task.Run(async () => await _behavior.HandleAsync(new GetInvoice(), ReadAfterOtherStarts, Ct), Ct);
        string? created = null;
        var create = Task.Run(async () => await _behavior.HandleAsync(new CreateInvoice(), async c =>
        {
            created = await ReadAfterOtherStarts(c);
            return 1;
        }, Ct), Ct);

        Assert.Equal("get grant", await get);
        await create;
        Assert.Equal("create grant", created);
    }

    [Fact]
    public async Task MessagePublishedByAHandler_DoesNotSeeThePublishersDecision()
    {
        var seen = new List<string?>();

        await _behavior.HandleAsync(new DeleteInvoice(1), async cancellationToken =>
        {
            await _behavior.HandleAsync(new InvoiceCreated(), _ =>
            {
                seen.Add(Details(_context.Current));
                return default;
            }, cancellationToken);
            seen.Add(Details(_context.Current));
        }, Ct);

        Assert.Equal([null, "delete grant"], seen);
    }

    [Fact]
    public async Task MessageWithoutDecision_RunsItsHandlers()
    {
        var ran = false;

        await _behavior.HandleAsync(new InvoiceCreated(), _ =>
        {
            ran = true;
            return default;
        }, Ct);

        Assert.True(ran);
    }

    [Fact]
    public async Task For_ReturnsTheDecisionOnlyForTheSameInstance()
    {
        var command = new DeleteInvoice(1);
        AuthorizationResult? same = null;
        AuthorizationResult? equalButOther = null;

        await _behavior.HandleAsync(command, _ =>
        {
            same = _context.GetResultFor(command);
            equalButOther = _context.GetResultFor(new DeleteInvoice(1));   // equal record, different instance
            return default;
        }, Ct);

        Assert.Same(command, same!.Action);
        Assert.Null(equalButOther);
        Assert.Null(_context.GetResultFor(command));
    }

    private static string? Details(AuthorizationResult? result) =>
        result is not null && result.TryGetDetails<string>(out var details) ? details : null;

    // Reads the decision after resuming, the way a handler reading the database between results would.
    private async IAsyncEnumerable<int> Stream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        _streamSaw.Add(Details(_context.Current));
        yield return 1;
        await Task.Delay(1, cancellationToken);
        _streamSaw.Add(Details(_context.Current));
        yield return 2;
    }
}
