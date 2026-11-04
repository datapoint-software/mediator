using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Datapoint.Mediator;

// How the adapters that take JSON arguments, the CLI and the MCP tools, find host-provided properties in them
// (ADR 0028). Linked as source into both.
internal static class HostProvidedNames
{
    // The JSON names the serializer binds host-provided properties to, including [JsonPropertyName]. They're compared
    // with OrdinalIgnoreCase, like System.Text.Json's case-insensitive binding, so "TenantId" can't slip past "tenantId".
    public static HashSet<string> Of(ActionDescriptor action, JsonSerializerOptions options)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (action.HostProvidedProperties.Count > 0)
        {
            foreach (var property in options.GetTypeInfo(action.ActionType).Properties)
            {
                if (property.AttributeProvider is PropertyInfo member && action.HostProvidedProperties.Any(p => p.Name == member.Name))
                {
                    names.Add(property.Name);
                }
            }
        }

        return names;
    }

    // Returns the contract's spelling of the name, never the caller's, so a message doesn't echo input. Arguments that
    // aren't a JSON object have no properties to find.
    public static string? FindIn(HashSet<string> names, JsonElement arguments)
    {
        if (names.Count == 0 || arguments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var argument in arguments.EnumerateObject())
        {
            if (names.TryGetValue(argument.Name, out var name))
            {
                return name;
            }
        }

        return null;
    }
}
