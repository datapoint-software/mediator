using System;
using System.Linq;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class ActionCatalogTests
{
    [Fact]
    public void Constructor_NullActionTypes_Throws()
    {
        Assert.Equal("actionTypes", Assert.Throws<ArgumentNullException>(() => new ActionCatalog(null!)).ParamName);
    }

    [Fact]
    public void Actions_ListsNamedActionsInNameOrder()
    {
        var catalog = new ActionCatalog([typeof(ZetaQuery), typeof(Unnamed), typeof(AlphaCommand), typeof(AlphaCommand)]);

        Assert.Equal(["alpha.command", "zeta.query"], catalog.Actions.Select(a => a.Name));
    }

    [Fact]
    public void Descriptors_DescribeEachKind()
    {
        var catalog = new ActionCatalog([typeof(AlphaCommand), typeof(ResultCommand), typeof(ZetaQuery), typeof(NumberStream)]);

        AssertDescriptor(catalog, "alpha.command", ActionKind.Command, typeof(AlphaCommand), null, "Does alpha.");
        AssertDescriptor(catalog, "result.command", ActionKind.CommandWithResult, typeof(ResultCommand), typeof(int), "A test action.");
        AssertDescriptor(catalog, "zeta.query", ActionKind.Query, typeof(ZetaQuery), typeof(string), "A test action.");
        AssertDescriptor(catalog, "number.stream", ActionKind.StreamQuery, typeof(NumberStream), typeof(int), "A test action.");
    }

    [Fact]
    public void TryGetAction_Unknown_ReturnsFalse()
    {
        var catalog = new ActionCatalog([typeof(AlphaCommand), typeof(Unnamed)]);

        Assert.False(catalog.TryGetAction("unknown", out var unknown));
        Assert.Null(unknown);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => catalog.TryGetAction(null!, out _)).ParamName);
    }

    [Theory]
    [InlineData(typeof(UppercaseName))]
    [InlineData(typeof(DoubleSeparatorName))]
    [InlineData(typeof(LeadingSeparatorName))]
    [InlineData(typeof(SpaceName))]
    [InlineData(typeof(EmptyName))]
    [InlineData(typeof(TooLongName))]
    [InlineData(typeof(UnderscoreName))]
    public void InvalidName_Throws(Type actionType)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ActionCatalog([actionType]));

        Assert.Contains("not a valid action name", exception.Message);
    }

    [Theory]
    [InlineData(typeof(EmptyDescription))]
    [InlineData(typeof(WhitespaceDescription))]
    public void MissingDescription_ThrowsNamingTheAction(Type actionType)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ActionCatalog([actionType]));

        Assert.Contains("'undescribed' on", exception.Message);
        Assert.Contains("has no description", exception.Message);
    }

    [Fact]
    public void DotsAndHyphens_AreValid()
    {
        Assert.True(new ActionCatalog([typeof(DotAndHyphenName)]).TryGetAction("invoices.create-draft", out _));
    }

    [Fact]
    public void DuplicateName_ThrowsNamingBothTypes()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ActionCatalog([typeof(AlphaCommand), typeof(AlphaDuplicate)]));

        Assert.Contains(nameof(AlphaCommand), exception.Message);
        Assert.Contains(nameof(AlphaDuplicate), exception.Message);
    }

    [Fact]
    public void ActionsWithoutChannel_ThrowListingEveryOne()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new ActionCatalog([typeof(AlphaCommand), typeof(Unoffered), typeof(OtherUnoffered)]));

        Assert.Contains("No host adapter offers", exception.Message);
        Assert.Contains($"'{typeof(Unoffered)}'", exception.Message);
        Assert.Contains($"'{typeof(OtherUnoffered)}'", exception.Message);
        Assert.DoesNotContain(nameof(AlphaCommand), exception.Message);
    }

    [Fact]
    public void CustomChannel_IsEnough()
    {
        Assert.True(new ActionCatalog([typeof(CustomOnly)]).TryGetAction("custom.only", out _));
    }

    [Fact]
    public void IsOfferedBy_MatchesExactlyTheMarkedChannels()
    {
        Assert.True(new ActionCatalog([typeof(TwoChannels), typeof(CustomOnly)]).TryGetAction("two.channels", out var action));

        Assert.True(action.IsOfferedBy<ApiAttribute>());
        Assert.True(action.IsOfferedBy<ModelContextProtocolAttribute>());
        Assert.False(action.IsOfferedBy<CommandLineAttribute>());
        Assert.False(action.IsOfferedBy<CustomChannelAttribute>());
        Assert.False(action.IsOfferedBy<ActionChannelAttribute>());
    }

    [Fact]
    public void IsOfferedBy_CustomChannel()
    {
        Assert.True(new ActionCatalog([typeof(CustomOnly)]).TryGetAction("custom.only", out var action));

        Assert.True(action.IsOfferedBy<CustomChannelAttribute>());
        Assert.False(action.IsOfferedBy<ApiAttribute>());
    }

    [Fact]
    public void NamedMessage_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ActionCatalog([typeof(NamedMessage)]));

        Assert.Contains("message", exception.Message);
    }

    [Fact]
    public void ActionWithSeveralActionInterfaces_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ActionCatalog([typeof(Ambiguous)]));
    }

    [Fact]
    public void ActionAttribute_And_ActionDescriptor_RejectNulls()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new ActionAttribute(null!, "d")).ParamName);
        Assert.Equal("description", Assert.Throws<ArgumentNullException>(() => new ActionAttribute("a", null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new ActionDescriptor(null!, "d", ActionKind.Command, typeof(AlphaCommand), null)).ParamName);
        Assert.Equal("description", Assert.Throws<ArgumentNullException>(() => new ActionDescriptor("a", null!, ActionKind.Command, typeof(AlphaCommand), null)).ParamName);
        Assert.Equal("actionType", Assert.Throws<ArgumentNullException>(() => new ActionDescriptor("a", "d", ActionKind.Command, null!, null)).ParamName);
    }

    private static void AssertDescriptor(ActionCatalog catalog, string name, ActionKind kind, Type actionType, Type? resultType, string description)
    {
        Assert.True(catalog.TryGetAction(name, out var action));
        Assert.Equal(name, action.Name);
        Assert.Equal(kind, action.Kind);
        Assert.Equal(actionType, action.ActionType);
        Assert.Equal(resultType, action.ResultType);
        Assert.Equal(description, action.Description);
    }

    [Api]
    [Action("alpha.command", "Does alpha.")]
    private sealed record AlphaCommand : ICommand;

    [Api]
    [Action("alpha.command", "A test action.")]
    private sealed record AlphaDuplicate : ICommand;

    [Api]
    [Action("result.command", "A test action.")]
    private sealed record ResultCommand : ICommand<int>;

    [Api]
    [Action("zeta.query", "A test action.")]
    private sealed record ZetaQuery : IQuery<string>;

    [Api]
    [Action("number.stream", "A test action.")]
    private sealed record NumberStream : IStreamQuery<int>;

    [Action("unoffered.command", "A test action.")]
    private sealed record Unoffered : ICommand;

    [Action("unoffered.other", "A test action.")]
    private sealed record OtherUnoffered : ICommand;

    [CustomChannel]
    [Action("custom.only", "A test action.")]
    private sealed record CustomOnly : ICommand;

    [Api]
    [ModelContextProtocol]
    [Action("two.channels", "A test action.")]
    private sealed record TwoChannels : ICommand;

    // A channel defined by a custom host adapter.
    private sealed class CustomChannelAttribute : ActionChannelAttribute;

    private sealed record Unnamed : ICommand;

    [Api]
    [Action("named.message", "A test action.")]
    private sealed record NamedMessage : IMessage;

    [Api]
    [Action("ambiguous", "A test action.")]
    private sealed record Ambiguous : ICommand<int>, IQuery<int>;

    [Api]
    [Action("Invoice.Create", "A test action.")]
    private sealed record UppercaseName : ICommand;

    [Api]
    [Action("invoice..create", "A test action.")]
    private sealed record DoubleSeparatorName : ICommand;

    [Api]
    [Action("-invoice", "A test action.")]
    private sealed record LeadingSeparatorName : ICommand;

    [Api]
    [Action("invoice create", "A test action.")]
    private sealed record SpaceName : ICommand;

    [Api]
    [Action("", "A test action.")]
    private sealed record EmptyName : ICommand;

    [Api]
    [Action("invoices_create", "A test action.")]
    private sealed record UnderscoreName : ICommand;

    [Api]
    [Action("invoices.create-draft", "A test action.")]
    private sealed record DotAndHyphenName : ICommand;

    [Api]
    [Action("a123456789a123456789a123456789a123456789a123456789a123456789abcde", "A test action.")]
    private sealed record TooLongName : ICommand;

    [Api]
    [Action("undescribed", "")]
    private sealed record EmptyDescription : ICommand;

    [Api]
    [Action("undescribed", " \t ")]
    private sealed record WhitespaceDescription : ICommand;
}
