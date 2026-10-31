using global::ModelContextProtocol.Server;
using Microsoft.Extensions.Options;

namespace Datapoint.Mediator.ModelContextProtocol;

// Adds a tool per catalog action when the MCP server's options are built, so it doesn't matter whether
// WithMediatorActions is called before or after AddMediator.
internal sealed class ActionToolsSetup(IActionCatalog catalog, MediatorMcpOptions options) : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions serverOptions)
    {
        var tools = serverOptions.ToolCollection ??= [];

        foreach (var action in catalog.Actions)
        {
            if (options.Filter?.Invoke(action) ?? true)
            {
                var tool = new ActionTool(action, options);
                tools.TryAdd(tool);
            }
        }
    }
}
