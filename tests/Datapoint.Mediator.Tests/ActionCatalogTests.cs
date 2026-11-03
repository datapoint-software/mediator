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
    public void Actions_ListsVisibleNamedActionsInNameOrder()
    {
        var catalog = new ActionCatalog([typeof(ZetaQuery), typeof(Unnamed), typeof(AlphaCommand), typeof(Hidden), typeof(AlphaCommand)]);

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
    public void TryGetAction_HiddenOrUnknown_ReturnsFalse()
    {
        var catalog = new ActionCatalog([typeof(Hidden)]);

        Assert.False(catalog.TryGetAction("hidden.command", out var hidden));
        Assert.Null(hidden);
        Assert.False(catalog.TryGetAction("unknown", out _));
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
    public void DuplicateName_IncludingHiddenAction_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ActionCatalog([typeof(Hidden), typeof(HiddenDuplicate)]));
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

    [Action("alpha.command", "Does alpha.")]
    private sealed record AlphaCommand : ICommand;

    [Action("alpha.command", "A test action.")]
    private sealed record AlphaDuplicate : ICommand;

    [Action("result.command", "A test action.")]
    private sealed record ResultCommand : ICommand<int>;

    [Action("zeta.query", "A test action.")]
    private sealed record ZetaQuery : IQuery<string>;

    [Action("number.stream", "A test action.")]
    private sealed record NumberStream : IStreamQuery<int>;

    [Action("hidden.command", "A test action.", Hidden = true)]
    private sealed record Hidden : ICommand;

    [Action("hidden.command", "A test action.")]
    private sealed record HiddenDuplicate : ICommand;

    private sealed record Unnamed : ICommand;

    [Action("named.message", "A test action.")]
    private sealed record NamedMessage : IMessage;

    [Action("ambiguous", "A test action.")]
    private sealed record Ambiguous : ICommand<int>, IQuery<int>;

    [Action("Invoice.Create", "A test action.")]
    private sealed record UppercaseName : ICommand;

    [Action("invoice..create", "A test action.")]
    private sealed record DoubleSeparatorName : ICommand;

    [Action("-invoice", "A test action.")]
    private sealed record LeadingSeparatorName : ICommand;

    [Action("invoice create", "A test action.")]
    private sealed record SpaceName : ICommand;

    [Action("", "A test action.")]
    private sealed record EmptyName : ICommand;

    [Action("invoices_create", "A test action.")]
    private sealed record UnderscoreName : ICommand;

    [Action("invoices.create-draft", "A test action.")]
    private sealed record DotAndHyphenName : ICommand;

    [Action("a123456789a123456789a123456789a123456789a123456789a123456789abcde", "A test action.")]
    private sealed record TooLongName : ICommand;

    [Action("undescribed", "")]
    private sealed record EmptyDescription : ICommand;

    [Action("undescribed", " \t ")]
    private sealed record WhitespaceDescription : ICommand;
}
