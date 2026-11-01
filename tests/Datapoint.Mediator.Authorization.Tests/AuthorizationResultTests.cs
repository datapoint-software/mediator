using System;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthorizationResultTests
{
    [Fact]
    public void NoDecisions_IsDenied()
    {
        var result = new AuthorizationResult(typeof(GetInvoice), []);

        Assert.False(result.IsAllowed);
        Assert.Equal(typeof(GetInvoice), result.ActionType);
        Assert.Empty(result.Decisions);
    }

    [Fact]
    public void AllAllowed_IsAllowed()
    {
        var result = new AuthorizationResult(typeof(GetInvoice), [Decision(AuthorizationDecision.Allow()), Decision(AuthorizationDecision.Allow("x"))]);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void AnyDenied_IsDenied()
    {
        var result = new AuthorizationResult(typeof(GetInvoice), [Decision(AuthorizationDecision.Allow()), Decision(AuthorizationDecision.Deny())]);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void TryGetDetails_ReturnsTheFirstDetailsOfThatType()
    {
        var grant = new ReadGrant("viewer", Watermark: true);
        var result = new AuthorizationResult(
            typeof(GetInvoice),
            [Decision(AuthorizationDecision.Allow("role rule")), Decision(AuthorizationDecision.Allow(grant)), Decision(AuthorizationDecision.Allow(new ReadGrant("other", false)))]);

        Assert.True(result.TryGetDetails<ReadGrant>(out var found));
        Assert.Same(grant, found);
        Assert.True(result.TryGetDetails<string>(out var text));
        Assert.Equal("role rule", text);
    }

    [Fact]
    public void TryGetDetails_NoDetailsOfThatType_ReturnsFalse()
    {
        var result = new AuthorizationResult(typeof(GetInvoice), [Decision(AuthorizationDecision.Allow())]);

        Assert.False(result.TryGetDetails<ReadGrant>(out var found));
        Assert.Null(found);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Equal("actionType", Assert.Throws<ArgumentNullException>(() => new AuthorizationResult(null!, [])).ParamName);
        Assert.Equal("decisions", Assert.Throws<ArgumentNullException>(() => new AuthorizationResult(typeof(GetInvoice), null!)).ParamName);
        Assert.Equal("authorizerType", Assert.Throws<ArgumentNullException>(() => new AuthorizerDecision(null!, AuthorizationDecision.Allow())).ParamName);
        Assert.Equal("decision", Assert.Throws<ArgumentNullException>(() => new AuthorizerDecision(typeof(object), null!)).ParamName);
        Assert.Equal("action", Assert.Throws<ArgumentNullException>(
            () => new AuthorizationEvent(null!, null, AuthorizationOutcome.Denied, null, null)).ParamName);
    }

    [Fact]
    public void AuthorizationEvent_KeepsItsValues()
    {
        var action = new GetInvoice();
        var principal = Principals.SignedIn();
        var result = new AuthorizationResult(typeof(GetInvoice), []);
        var exception = new InvalidOperationException();

        var authorization = new AuthorizationEvent(action, principal, AuthorizationOutcome.Failed, result, exception);

        Assert.Same(action, authorization.Action);
        Assert.Same(principal, authorization.Principal);
        Assert.Equal(AuthorizationOutcome.Failed, authorization.Outcome);
        Assert.Same(result, authorization.Result);
        Assert.Same(exception, authorization.Exception);
    }

    private static AuthorizerDecision Decision(AuthorizationDecision decision) => new(typeof(object), decision);
}
