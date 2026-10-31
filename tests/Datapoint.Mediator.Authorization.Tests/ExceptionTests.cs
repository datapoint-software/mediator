using System;
using Xunit;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed class ExceptionTests
{
    [Fact]
    public void AuthenticationRequiredException_Constructors()
    {
        var inner = new InvalidOperationException();

        Assert.Equal("Authentication is required.", new AuthenticationRequiredException().Message);
        Assert.Equal("message", new AuthenticationRequiredException("message").Message);
        var withInner = new AuthenticationRequiredException("message", inner);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void AccessDeniedException_Constructors()
    {
        var inner = new InvalidOperationException();

        Assert.Equal("Access is denied.", new AccessDeniedException().Message);
        Assert.Equal("message", new AccessDeniedException("message").Message);
        var withInner = new AccessDeniedException("message", inner);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }
}
