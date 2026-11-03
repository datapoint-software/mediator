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
// walk stops before anything the contract doesn't describe. It runs only when deserialization has already failed.
internal static class ArgumentField
{
    public static string? Find(JsonException exception, JsonElement arguments, JsonTypeInfo typeInfo)
    {
        // System.Text.Json sets the path on every exception it throws while deserializing, starting with "$".
        var segments = Parse(exception.Path ?? "$", out var complete);
        var field = new StringBuilder();
        JsonElement? element = arguments;

        foreach (var segment in segments)
        {
            // Dictionaries, polymorphic types and custom converters: the next segment isn't in the contract.
            if (segment.Name is null ? typeInfo.Kind != JsonTypeInfoKind.Enumerable : !IsPlainObject(typeInfo))
            {
                complete = false;
                break;
            }

            if (segment.Name is null)
            {
                field.Append('[').Append(segment.Index.ToString(CultureInfo.InvariantCulture)).Append(']');
                typeInfo = typeInfo.Options.GetTypeInfo(typeInfo.ElementType!);
                element = element is { ValueKind: JsonValueKind.Array } array && segment.Index < array.GetArrayLength()
                    ? array[segment.Index]
                    : null;
            }
            else
            {
                // A name the contract doesn't have is an unknown property, which the caller named.
                if (FindProperty(typeInfo, segment.Name) is not { } property)
                {
                    return null;
                }

                Append(field, property.Name);
                typeInfo = typeInfo.Options.GetTypeInfo(property.PropertyType);
                element = element is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(segment.Name, out var value)
                    ? value
                    : null;
            }
        }

        // A missing required property is reported at the object that lacks it.
        if (complete && IsPlainObject(typeInfo) && element is { ValueKind: JsonValueKind.Object } target
            && FindMissingRequired(typeInfo, target) is { } missing)
        {
            Append(field, missing.Name);
        }

        return field.Length > 0 ? field.ToString() : null;
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
        var i = 1;
        int end;

        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                end = path.IndexOfAny(['.', '['], i + 1);
                end = end < 0 ? path.Length : end;
                segments.Add(new Segment(path[(i + 1)..end], 0));
                i = end;
            }
            else if (path.AsSpan(i).StartsWith("['", StringComparison.Ordinal) && (end = path.IndexOf("']", i + 2, StringComparison.Ordinal)) >= 0)
            {
                segments.Add(new Segment(path[(i + 2)..end], 0));
                i = end + 2;
            }
            else if (path[i] == '[' && (end = path.IndexOf(']', i + 1)) >= 0
                && int.TryParse(path.AsSpan(i + 1, end - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                segments.Add(new Segment(null, index));
                i = end + 1;
            }
            else
            {
                complete = false;
                return segments;
            }
        }

        complete = true;
        return segments;
    }

    // A property name, or an array index when Name is null.
    private readonly record struct Segment(string? Name, int Index);
}
