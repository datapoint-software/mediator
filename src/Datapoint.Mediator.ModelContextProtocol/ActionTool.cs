using System;
using System.Collections.Generic;
using System.Linq;
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
        _hostProvided = HostProvidedNames.Of(action, _serializerOptions);

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
        if (HostProvidedNames.FindIn(_hostProvided, arguments) is { } name)
        {
            return Error(AdapterMessages.HostProvided(name));
        }

        var dispatcher = services.GetRequiredService<IActionDispatcher>();

        try
        {
            JsonObject content;

            if (_action.Kind == ActionKind.StreamQuery)
            {
                content = await StreamAsync(dispatcher, arguments, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var result = await dispatcher.DispatchAsync(_action.Name, arguments, _serializerOptions, cancellationToken).ConfigureAwait(false);
                content = new JsonObject { ["result"] = JsonSerializer.SerializeToNode(result, _serializerOptions) };
            }

            var structured = JsonSerializer.SerializeToElement(content, _serializerOptions);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = structured.GetRawText() }],
                StructuredContent = structured,
            };
        }
        catch (Exception exception) when (!AdapterErrors.IsCallerCancellation(exception, cancellationToken))
        {
            return ErrorResult(exception);
        }
    }

    private async Task<JsonObject> StreamAsync(IActionDispatcher dispatcher, JsonElement arguments, CancellationToken cancellationToken)
    {
        var items = new JsonArray();
        var truncated = false;

        await foreach (var item in dispatcher.StreamAsync(_action.Name, arguments, _serializerOptions, cancellationToken).ConfigureAwait(false))
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

    // Host-provided properties are left out of the schema, so an agent is never asked for them.
    private JsonElement WithoutHostProvided(JsonElement schema)
    {
        if (_hostProvided.Count == 0)
        {
            return schema;
        }

        // Not null: the SDK always describes an action's arguments as a JSON object.
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
            // A schema's required list only holds property names, never null.
            foreach (var item in required.Where(r => _hostProvided.Contains(r!.GetValue<string>())).ToList())
            {
                required.Remove(item);
            }
        }

        return JsonSerializer.SerializeToElement(json, AIJsonUtilities.DefaultOptions);
    }

    // Errors are returned as results, not thrown, so the MCP SDK doesn't prefix their text. Content from the app's hook
    // is sent both as structured content and as its JSON text, as MCP recommends for clients that only read text.
    // Nothing is ever rethrown, because the SDK sends an McpException's message to the agent.
    private CallToolResult ErrorResult(Exception exception)
    {
        if (CallHook(() => _options.ErrorContent?.Invoke(exception)) is { } content)
        {
            // Content that isn't a JSON object is a bug in the hook, not something to show the agent.
            return Serialize(content) is { ValueKind: JsonValueKind.Object } structured
                ? Error(structured.GetRawText(), structured)
                : Error(AdapterErrors.UnexpectedError);
        }

        return Error(SafeMessage(exception) ?? AdapterErrors.UnexpectedError);
    }

    // An exception from the app's hook is treated as if the hook had returned null, because nothing about it is safe to
    // show the agent.
    private static T? CallHook<T>(Func<T?> hook)
        where T : class
    {
        try
        {
            return hook();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private JsonElement? Serialize(object content)
    {
        try
        {
            return JsonSerializer.SerializeToElement(content, content.GetType(), _serializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CallToolResult Error(string text, JsonElement? structured = null) =>
        new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structured,
        };

    // Only messages known to be safe reach the agent. Invalid arguments and authorization failures go to the app's hook
    // first, so it can report them with its own codes. Anything else gets null, and a generic error.
    private string? SafeMessage(Exception exception)
    {
        var message = CallHook(() => _options.ErrorMessage?.Invoke(exception));

        return exception switch
        {
            InvalidActionArgumentsException invalid => message ?? AdapterMessages.InvalidArguments(_action.Name, invalid),
            _ when AdapterErrors.IsAuthenticationRequired(exception) => message ?? AdapterErrors.AuthenticationRequired,
            _ when AdapterErrors.IsAccessDenied(exception) => message ?? AdapterErrors.AccessDenied,
            _ => message,
        };
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNamePattern();
}
