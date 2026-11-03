using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class ActionArgumentsTests
{
    private const string Id = "\"id\": \"3f2c0d4e-0000-0000-0000-000000000000\"";

    private static readonly ActionDescriptor Action = new("orders.create", null, ActionKind.Command, typeof(CreateOrder), null);

    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Deserialize_ValidArguments_ReturnsTheAction()
    {
        var order = Assert.IsType<CreateOrder>(ActionArguments.Deserialize(Action, Json($$"""{ "count": 2, {{Id}}, "name": "Ana" }""")));

        Assert.Equal(2, order.Count);
        Assert.Equal("Ana", order.Name);
    }

    [Fact]
    public void Deserialize_UndefinedArguments_AreAnEmptyObject()
    {
        var order = Assert.IsType<CreateOrder>(ActionArguments.Deserialize(Action, default));

        Assert.Equal(0, order.Count);
    }

    [Fact]
    public void Deserialize_NullArguments_ThrowsWithoutField()
    {
        var exception = Assert.Throws<InvalidActionArgumentsException>(() => ActionArguments.Deserialize(Action, Json("null")));

        Assert.Equal("The arguments for action 'orders.create' are missing.", exception.Message);
        Assert.Equal("orders.create", exception.ActionName);
        Assert.Null(exception.Field);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Deserialize_NullAction_Throws()
    {
        Assert.Equal("action", Assert.Throws<ArgumentNullException>(() => ActionArguments.Deserialize(null!, default)).ParamName);
    }

    [Theory]
    [InlineData("wrong type", """{ "count": "secret-value", "name": "Ana" }""", "count")]
    [InlineData("invalid GUID", """{ "id": "secret-value", "name": "Ana" }""", "id")]
    [InlineData("caller's spelling", """{ "COUNT": "secret-value" }""", "count")]
    [InlineData("nested property", """{ "address": { "city": { "postcode": "secret-value" } } }""", "address.city.postcode")]
    [InlineData("array item", """{ "lines": [ { "quantity": 1 }, { "quantity": "secret-value" } ] }""", "lines[1].quantity")]
    [InlineData("whole array", """{ "lines": "secret-value" }""", "lines")]
    [InlineData("dictionary key", """{ "tags": { "secret-value": "x" } }""", "tags")]
    [InlineData("dictionary key with \"']\"", """{ "tags": { "secret']value": "x" } }""", "tags")]
    [InlineData("item of a dictionary", """{ "groups": { "secret-value": [ "x" ] } }""", "groups")]
    [InlineData("polymorphic value", """{ "payment": { "$type": "card", "number": "secret-value" } }""", "payment")]
    [InlineData("custom converter", """{ "code": 123 }""", "code")]
    public void Deserialize_InvalidArgument_NamesTheFieldWithWebOptions(string situation, string json, string field)
    {
        var exception = Assert.Throws<InvalidActionArgumentsException>(() => ActionArguments.Deserialize(Action, Json(json)));

        AssertField(exception, field, situation);
    }

    [Theory]
    [InlineData("missing required property", $$"""{ "count": 1 }""", "id")]
    [InlineData("null for a non-nullable property", $$"""{ "count": 1, {{Id}}, "name": null }""", "name")]
    [InlineData("missing nested required property", $$"""{ "count": 1, {{Id}}, "name": "Ana", "lines": [ {} ] }""", "lines[0].quantity")]
    [InlineData("missing required member", $$"""{ "count": 1, {{Id}}, "name": "Ana", "customer": {} }""", "customer.email")]
    [InlineData("duplicate property", $$"""{ "count": 1, "count": 2, {{Id}}, "name": "Ana" }""", "count")]
    public void Deserialize_InvalidArgument_NamesTheFieldWithStrictOptions(string situation, string json, string field)
    {
        var exception = Assert.Throws<InvalidActionArgumentsException>(() => ActionArguments.Deserialize(Action, Json(json), Strict));

        AssertField(exception, field, situation);
    }

    [Theory]
    [InlineData("""{ "count": 1, "secret-value": 1 }""")]
    [InlineData("""{ "COUNT": 1 }""")]
    [InlineData("""{ "count": 1, "address": { "secret-value": 1 } }""")]
    public void Deserialize_UnknownProperty_ThrowsWithoutField(string json)
    {
        var exception = Assert.Throws<InvalidActionArgumentsException>(() => ActionArguments.Deserialize(Action, Json(json), Strict));

        Assert.Null(exception.Field);
        Assert.Equal("The arguments for action 'orders.create' are not valid.", exception.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"secret-value\"")]
    public void Deserialize_ArgumentsNotAnObject_ThrowsWithoutField(string json)
    {
        var exception = Assert.Throws<InvalidActionArgumentsException>(() => ActionArguments.Deserialize(Action, Json(json)));

        Assert.Null(exception.Field);
        Assert.DoesNotContain("secret-value", exception.Message);
    }

    private static void AssertField(InvalidActionArgumentsException exception, string field, string situation)
    {
        Assert.True(field == exception.Field, $"{situation}: expected '{field}', got '{exception.Field}'.");
        Assert.Equal("orders.create", exception.ActionName);
        Assert.Equal($"The argument '{field}' of action 'orders.create' is not valid.", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
        Assert.DoesNotContain("System.", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed record CreateOrder(
        int Count,
        Guid Id,
        string Name,
        Address? Address = null,
        Line[]? Lines = null,
        Dictionary<string, int>? Tags = null,
        Dictionary<string, int[]>? Groups = null,
        Payment? Payment = null,
        Customer? Customer = null,
        [property: JsonConverter(typeof(StrictStringConverter))] string? Code = null) : ICommand;

    private sealed record Address(City City);

    private sealed record City(int Postcode);

    private sealed record Line(int Quantity);

    private sealed class Customer
    {
        public required string Email { get; init; }
    }

    [JsonDerivedType(typeof(CardPayment), "card")]
    private record Payment;

    private sealed record CardPayment(int Number) : Payment;

    // Refuses anything but a string, as a custom converter would.
    private sealed class StrictStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? reader.GetString()! : throw new JsonException();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}
