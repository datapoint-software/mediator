using System;
using System.Collections.Generic;
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

    public ActionTool(ActionDescriptor action, MediatorMcpOptions options)
    {
        _action = action;
        _options = options;
        _serializerOptions = options.SerializerOptions ?? JsonSerializerOptions.Web;

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
            InputSchema = AIJsonUtilities.CreateJsonSchema(action.ActionType, serializerOptions: _serializerOptions),
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
        var dispatcher = services.GetRequiredService<IActionDispatcher>();
        var arguments = JsonSerializer.SerializeToElement(
            request.Params?.Arguments ?? new Dictionary<string, JsonElement>(),
            _serializerOptions);

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
        catch (Exception exception) when (exception is not OperationCanceledException && SafeMessage(exception) is { } message)
        {
            throw new McpException(message);
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

    // Only messages known to be safe reach the agent. Anything else is rethrown, and the MCP SDK reports a generic
    // error without the exception's message.
    private string? SafeMessage(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException => exception.Message,
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AuthenticationRequiredException" =>
                "Authentication is required.",
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AccessDeniedException" =>
                "Access is denied.",
            _ => _options.ErrorMessage?.Invoke(exception),
        };

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNamePattern();
}
