using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class AuthorizationDecisionTests
{
    [Fact]
    public void Allow_WithoutDetails_IsAllowedAndShared()
    {
        var decision = AuthorizationDecision.Allow();

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.Details);
        Assert.Same(decision, AuthorizationDecision.Allow());
    }

    [Fact]
    public void Allow_WithDetails_KeepsThem()
    {
        var details = new object();

        var decision = AuthorizationDecision.Allow(details);

        Assert.True(decision.IsAllowed);
        Assert.Same(details, decision.Details);
    }

    [Fact]
    public void Deny_WithoutDetails_IsDeniedAndShared()
    {
        var decision = AuthorizationDecision.Deny();

        Assert.False(decision.IsAllowed);
        Assert.Null(decision.Details);
        Assert.Same(decision, AuthorizationDecision.Deny());
    }

    [Fact]
    public void Deny_WithDetails_KeepsThem()
    {
        var decision = AuthorizationDecision.Deny("revoked");

        Assert.False(decision.IsAllowed);
        Assert.Equal("revoked", decision.Details);
    }

    [Fact]
    public void AllowIf_FollowsTheCondition()
    {
        Assert.Same(AuthorizationDecision.Allow(), AuthorizationDecision.AllowIf(true));
        Assert.Same(AuthorizationDecision.Deny(), AuthorizationDecision.AllowIf(false));
    }
}
