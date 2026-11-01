using System.Security.Claims;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class FixedPrincipalAccessorTests
{
    [Fact]
    public void Principal_ReturnsTheGivenUser()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));

        Assert.Same(principal, new FixedPrincipalAccessor(principal).Principal);
    }

    [Fact]
    public void Principal_WithoutUser_IsNull()
    {
        Assert.Null(new FixedPrincipalAccessor(null).Principal);
    }
}
