using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Datapoint.Mediator;

// Finds the argument a JsonException is about, from its Path and the action's contract. The path holds the caller's
// spelling and dictionary keys, so only names found in the contract are written, in the contract's spelling, and the
// walk stops before anything the contract doesn't describe. The reason comes from the contract and the kind of JSON
// value only. It runs only when deserialization has already failed.
internal static class ArgumentField
{
    public static string? Find(JsonException exception, JsonElement arguments, JsonTypeInfo typeInfo, out InvalidActionArgumentReason? reason)
    {
        // System.Text.Json sets the path on every exception it throws while deserializing, starting with "$".
        var segments = Parse(exception.Path ?? "$", out var complete);
        var field = new StringBuilder();
        JsonElement? element = arguments;

        foreach (var segment in segments)
        {
            // Dictionaries, polymorphic types and custom converters: the next segment isn't in the contract.
            if (!CanStepInto(typeInfo, segment))
            {
                complete = false;
                break;
            }

            if (segment.Name is null)
            {
                StepIntoItem(segment.Index, field, ref typeInfo, ref element);
            }
            else if (!TryStepIntoProperty(segment.Name, field, ref typeInfo, ref element))
            {
                // A name the contract doesn't have is an unknown property, which the caller named.
                reason = null;
                return null;
            }
        }

        reason = GetReason(complete, field, typeInfo, element);

        if (field.Length == 0)
        {
            reason = null;
            return null;
        }

        return field.ToString();
    }

    private static bool CanStepInto(JsonTypeInfo typeInfo, Segment segment)
    {
        if (segment.Name is null)
        {
            return typeInfo.Kind == JsonTypeInfoKind.Enumerable;
        }

        return IsPlainObject(typeInfo);
    }

    private static void StepIntoItem(int index, StringBuilder field, ref JsonTypeInfo typeInfo, ref JsonElement? element)
    {
        field.Append('[').Append(index.ToString(CultureInfo.InvariantCulture)).Append(']');

        // An enumerable's contract always has an element type.
        typeInfo = typeInfo.Options.GetTypeInfo(typeInfo.ElementType!);

        // The path comes from the JSON being read, so the item is there. Null is only a safeguard.
        element = element is { ValueKind: JsonValueKind.Array } array && index < array.GetArrayLength() ? array[index] : null;
    }

    private static bool TryStepIntoProperty(string name, StringBuilder field, ref JsonTypeInfo typeInfo, ref JsonElement? element)
    {
        if (FindProperty(typeInfo, name) is not { } property)
        {
            return false;
        }

        Append(field, property.Name);
        typeInfo = typeInfo.Options.GetTypeInfo(property.PropertyType);

        // The path comes from the JSON being read, so the property is there. Null is only a safeguard.
        element = element is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(name, out var value) ? value : null;

        return true;
    }

    // A missing required property is reported at the object that lacks it, and its name is added to the field. Only a
    // complete walk reaches the value that failed. Otherwise, the element is the value's container.
    private static InvalidActionArgumentReason GetReason(bool complete, StringBuilder field, JsonTypeInfo typeInfo, JsonElement? element)
    {
        if (!complete)
        {
            return InvalidActionArgumentReason.Invalid;
        }

        if (IsPlainObject(typeInfo) && element is { ValueKind: JsonValueKind.Object } target
            && FindMissingRequired(typeInfo, target) is { } missing)
        {
            Append(field, missing.Name);
            return InvalidActionArgumentReason.Missing;
        }

        return element is { ValueKind: JsonValueKind.Null } ? InvalidActionArgumentReason.Null : InvalidActionArgumentReason.Invalid;
    }

    private static void Append(StringBuilder field, string name)
    {
        if (field.Length > 0)
        {
            field.Append('.');
        }

        field.Append(name);
    }

    private static bool IsPlainObject(JsonTypeInfo typeInfo) =>
        typeInfo.Kind == JsonTypeInfoKind.Object && typeInfo.PolymorphismOptions is null;

    private static JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, string name)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (string.Equals(property.Name, name, Comparison(typeInfo)))
            {
                return property;
            }
        }

        return null;
    }

    private static JsonPropertyInfo? FindMissingRequired(JsonTypeInfo typeInfo, JsonElement json) =>
        typeInfo.Properties.FirstOrDefault(property => property.IsRequired
            && !json.EnumerateObject().Any(member => string.Equals(member.Name, property.Name, Comparison(typeInfo))));

    // Matches property names the way the serializer binds them.
    private static StringComparison Comparison(JsonTypeInfo typeInfo) =>
        typeInfo.Options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Parses the ".name", "['name']" and "[0]" segments that follow the path's "$". Parsing stops at anything else,
    // such as a dictionary key containing "']", and complete is then false: the walk can't follow the rest of the path.
    private static List<Segment> Parse(string path, out bool complete)
    {
        var segments = new List<Segment>();
        var position = 1;

        while (position < path.Length)
        {
            if (!TryParseSegment(path, ref position, out var segment))
            {
                complete = false;
                return segments;
            }

            segments.Add(segment);
        }

        complete = true;
        return segments;
    }

    // Reads the segment at position, and moves position past it.
    private static bool TryParseSegment(string path, ref int position, out Segment segment)
    {
        int end;

        if (path[position] == '.')
        {
            end = path.IndexOfAny(['.', '['], position + 1);

            if (end < 0)
            {
                end = path.Length;
            }

            segment = new Segment(path[(position + 1)..end], 0);
            position = end;
            return true;
        }

        if (path.AsSpan(position).StartsWith("['", StringComparison.Ordinal))
        {
            end = path.IndexOf("']", position + 2, StringComparison.Ordinal);

            if (end >= 0)
            {
                segment = new Segment(path[(position + 2)..end], 0);
                position = end + 2;
                return true;
            }
        }
        else if (path[position] == '[')
        {
            end = path.IndexOf(']', position + 1);

            if (end >= 0 && int.TryParse(path.AsSpan(position + 1, end - position - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                segment = new Segment(null, index);
                position = end + 1;
                return true;
            }
        }

        segment = default;
        return false;
    }

    // A property name, or an array index when Name is null.
    private readonly record struct Segment(string? Name, int Index);
}
