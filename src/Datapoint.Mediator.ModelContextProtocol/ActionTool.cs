using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::ModelContextProtocol;
using global::ModelContextProtocol.Protocol;
using global::ModelContextProtocol.Server;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Datapoint.Mediator.ModelContextProtocol;

// Offers one action as an MCP tool. Calls are dispatched by name, so every behaviour runs.
internal sealed partial class ActionTool : McpServerTool
{
    private readonly ActionDescriptor _action;
    private readonly MediatorMcpOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly HashSet<string> _hostProvided;

    public ActionTool(ActionDescriptor action, MediatorMcpOptions options)
    {
        _action = action;
        _options = options;
        _serializerOptions = options.SerializerOptions ?? JsonSerializerOptions.Web;
        _hostProvided = HostProvidedJsonNames(action, _serializerOptions);

        // Many AI model APIs only accept tool names made of letters, digits, underscores and hyphens. Action names
        // can't contain underscores, so replacing dots with underscores can't make two tools share a name.
        var name = action.Name.Replace('.', '_');

        if (!ToolNamePattern().IsMatch(name))
        {
            throw new InvalidOperationException(
                $"Action '{action.Name}' can't be offered as an MCP tool, because '{name}' is not a valid tool name.");
        }

        var readOnly = action.Kind is ActionKind.Query or ActionKind.StreamQuery;

        ProtocolTool = new Tool
        {
            Name = name,
            Description = action.Description,
            InputSchema = WithoutHostProvided(AIJsonUtilities.CreateJsonSchema(action.ActionType, serializerOptions: _serializerOptions)),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = readOnly,
                DestructiveHint = readOnly ? null : true,
            },
        };
    }

    public override Tool ProtocolTool { get; }

    public override IReadOnlyList<object> Metadata { get; } = [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var services = request.Services
            ?? throw new InvalidOperationException("The MCP request has no service provider to resolve the action dispatcher from.");

        var arguments = JsonSerializer.SerializeToElement(
            request.Params?.Arguments ?? new Dictionary<string, JsonElement>(),
            _serializerOptions);

        // Checked on the arguments that are dispatched, not on the agent's keys, because serializing applies the app's
        // DictionaryKeyPolicy: with snake_case, "TenantId" would pass a check on the keys, then become "tenant_id".
        // Checked before the dispatcher is resolved, so a decorator of the dispatcher can still set the value.
        if (FindHostProvided(arguments) is { } name)
        {
            return Error($"'{name}' is set by the server and cannot be given.");
        }

        var dispatcher = services.GetRequiredService<IActionDispatcher>();

        try
        {
            var content = _action.Kind == ActionKind.StreamQuery
                ? await StreamAsync(dispatcher, arguments, cancellationToken).ConfigureAwait(false)
                : new JsonObject
                {
                    ["result"] = JsonSerializer.SerializeToNode(
                        await dispatcher.DispatchAsync(_action.Name, arguments, _serializerOptions, cancellationToken).ConfigureAwait(false),
                        _serializerOptions),
                };

            var structured = JsonSerializer.SerializeToElement(content, _serializerOptions);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = structured.GetRawText() }],
                StructuredContent = structured,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (ErrorResult(exception) is { } error)
            {
                return error;
            }

            throw;
        }
    }

    private async Task<JsonObject> StreamAsync(IActionDispatcher dispatcher, JsonElement arguments, CancellationToken ct)
    {
        var items = new JsonArray();
        var truncated = false;

        await foreach (var item in dispatcher.StreamAsync(_action.Name, arguments, _serializerOptions, ct).ConfigureAwait(false))
        {
            if (items.Count == _options.MaxStreamResults)
            {
                // Stopping the enumeration disposes the handler's stream, so it produces nothing more.
                truncated = true;
                break;
            }

            items.Add(JsonSerializer.SerializeToNode(item, _serializerOptions));
        }

        return new JsonObject { ["items"] = items, ["truncated"] = truncated };
    }

    // The JSON names the serializer binds host-provided properties to, including [JsonPropertyName]. They're compared
    // with OrdinalIgnoreCase, like System.Text.Json's case-insensitive binding, so "TenantId" can't slip past "tenantId".
    private static HashSet<string> HostProvidedJsonNames(ActionDescriptor action, JsonSerializerOptions options)
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

    // Host-provided properties are left out of the schema, so an agent is never asked for them.
    private JsonElement WithoutHostProvided(JsonElement schema)
    {
        if (_hostProvided.Count == 0)
        {
            return schema;
        }

        var json = JsonObject.Create(schema)!;

        if (json["properties"] is JsonObject properties)
        {
            foreach (var key in properties.Select(p => p.Key).Where(_hostProvided.Contains).ToList())
            {
                properties.Remove(key);
            }
        }

        if (json["required"] is JsonArray required)
        {
            foreach (var item in required.Where(r => _hostProvided.Contains(r!.GetValue<string>())).ToList())
            {
                required.Remove(item);
            }
        }

        return JsonSerializer.SerializeToElement(json, AIJsonUtilities.DefaultOptions);
    }

    // Returns the contract's spelling of the name, never the agent's, so the error doesn't echo input.
    // The arguments are a serialized dictionary, so always a JSON object.
    private string? FindHostProvided(JsonElement arguments)
    {
        foreach (var property in arguments.EnumerateObject())
        {
            if (_hostProvided.TryGetValue(property.Name, out var name))
            {
                return name;
            }
        }

        return null;
    }

    // Errors are returned as results, not thrown, so the MCP SDK doesn't prefix their text. Content from the app's hook
    // is sent both as structured content and as its JSON text, as MCP recommends for clients that only read text.
    private CallToolResult? ErrorResult(Exception exception)
    {
        if (_options.ErrorContent?.Invoke(exception) is { } content)
        {
            var structured = JsonSerializer.SerializeToElement(content, content.GetType(), _serializerOptions);

            // Thrown from the catch block, so the MCP SDK reports a generic error without this message.
            return structured.ValueKind == JsonValueKind.Object
                ? Error(structured.GetRawText(), structured)
                : throw new InvalidOperationException("MediatorMcpOptions.ErrorContent must return a value that serializes to a JSON object.");
        }

        return SafeMessage(exception) is { } message ? Error(message) : null;
    }

    private static CallToolResult Error(string text, JsonElement? structured = null) =>
        new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structured,
        };

    // Only messages known to be safe reach the agent. Invalid arguments and authorization failures go to the app's hook
    // first, so it can report them with its own codes. Anything else is rethrown, and the MCP SDK reports a generic
    // error without the exception's message.
    private string? SafeMessage(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException invalid => _options.ErrorMessage?.Invoke(exception) ?? InvalidArguments(invalid),
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AuthenticationRequiredException" =>
                _options.ErrorMessage?.Invoke(exception) ?? "Authentication is required.",
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AccessDeniedException" =>
                _options.ErrorMessage?.Invoke(exception) ?? "Access is denied.",
            _ => _options.ErrorMessage?.Invoke(exception),
        };

    // Built from the field and the reason only, never from the exception's message. A field that doesn't look like a path of property
    // names, which an app's own exception could carry, is left out.
    private string InvalidArguments(InvalidActionArgumentsException exception) =>
        exception.Field is { Length: <= 128 } field && FieldPattern().IsMatch(field)
            ? $"The argument '{field}' of action '{_action.Name}' is {(IsRequired(exception) ? "required" : "not valid")}."
            : $"The arguments for action '{_action.Name}' are not valid.";

    // A missing or null argument is reported as required.
    private static bool IsRequired(InvalidActionArgumentsException exception) =>
        exception.Reason is InvalidActionArgumentReason.Missing or InvalidActionArgumentReason.Null;

    [GeneratedRegex(@"^[A-Za-z0-9_$-]+(\[[0-9]+\])*(\.[A-Za-z0-9_$-]+(\[[0-9]+\])*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPattern();

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNamePattern();
}
