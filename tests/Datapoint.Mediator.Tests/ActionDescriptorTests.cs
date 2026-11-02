using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class ActionDescriptorTests
{
    [Fact]
    public void HostProvidedProperties_NoneMarked_IsEmpty()
    {
        Assert.Empty(Describe(typeof(Unmarked)).HostProvidedProperties);
    }

    [Fact]
    public void HostProvidedProperties_FindsMarkedProperty()
    {
        Assert.Equal(["TenantId"], Names(typeof(MarkedProperty)));
    }

    [Fact]
    public void HostProvidedProperties_FindsMarkedRecordParameter()
    {
        Assert.Equal(["TenantId"], Names(typeof(MarkedParameter)));
    }

    [Fact]
    public void HostProvidedProperties_FindsPropertyMarkedOnABaseType()
    {
        Assert.Equal(["TenantId"], Names(typeof(DerivedFromMarked)));
    }

    [Fact]
    public void HostProvidedProperties_IgnoresIndexers()
    {
        Assert.Empty(Describe(typeof(MarkedIndexer)).HostProvidedProperties);
    }

    [Fact]
    public void HostProvidedProperties_CannotBeChanged()
    {
        var properties = Assert.IsAssignableFrom<IList<System.Reflection.PropertyInfo>>(Describe(typeof(MarkedProperty)).HostProvidedProperties);

        Assert.Throws<NotSupportedException>(() => properties[0] = null!);
    }

    [Fact]
    public void Reason_IsKept()
    {
        Assert.Equal("Set by the test.", new HostProvidedAttribute { Reason = "Set by the test." }.Reason);
        Assert.Null(new HostProvidedAttribute().Reason);
    }

    private static ActionDescriptor Describe(Type actionType) =>
        new("test.action", null, ActionKind.Command, actionType, null);

    private static IEnumerable<string> Names(Type actionType) =>
        Describe(actionType).HostProvidedProperties.Select(property => property.Name);

    private sealed record Unmarked(string Name) : ICommand;

    private sealed record MarkedProperty(string Name) : ICommand
    {
        [HostProvided]
        public Guid TenantId { get; init; }
    }

    private sealed record MarkedParameter([HostProvided(Reason = "The caller's tenant.")] Guid TenantId, string Name) : ICommand;

    private class MarkedBase
    {
        [HostProvided]
        public virtual Guid TenantId { get; init; }
    }

    private sealed class DerivedFromMarked : MarkedBase, ICommand
    {
        public override Guid TenantId { get; init; }

        public string? Name { get; init; }
    }

    private sealed class MarkedIndexer : ICommand
    {
        [HostProvided]
        public int this[int index] => index;
    }
}
