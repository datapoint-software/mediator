using System;
using Datapoint.Mediator;
using Datapoint.Mediator.ModelContextProtocol;
using global::ModelContextProtocol.Server;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for offering mediator actions as MCP tools.
/// </summary>
public static class MediatorMcpServerBuilderExtensions
{
    /// <summary>
    /// Offers every action in <see cref="IActionCatalog"/> as an MCP tool.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">Configures which actions are offered and how, or <see langword="null"/>.</param>
    /// <returns>The MCP server builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each tool is named after its action, with dots replaced by underscores, so <c>invoices.create</c> becomes
    /// <c>invoices_create</c>. Its description and input schema come from the action, and queries are marked as
    /// read-only while commands are marked as possibly destructive, so clients can ask the user before running them.
    /// </para>
    /// <para>
    /// Calls are dispatched through <see cref="IActionDispatcher"/>, so every behaviour runs, including authorization.
    /// Errors are returned as tool results marked as errors. Authorization and argument errors are reported to the agent
    /// with fixed, safe messages; other exceptions are reported as a generic error, unless
    /// <see cref="MediatorMcpOptions.ErrorMessage"/> or <see cref="MediatorMcpOptions.ErrorContent"/> describes them.
    /// </para>
    /// <para>
    /// The mediator must also be registered with <c>AddMediator</c>. Calling this method more than once doesn't offer
    /// the same tool twice.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MediatorMcpOptions.MaxStreamResults"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">
    /// Both <see cref="MediatorMcpOptions.ErrorMessage"/> and <see cref="MediatorMcpOptions.ErrorContent"/> are set.
    /// </exception>
    public static IMcpServerBuilder WithMediatorActions(this IMcpServerBuilder builder, Action<MediatorMcpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new MediatorMcpOptions();
        configure?.Invoke(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxStreamResults, 1, "options.MaxStreamResults");

        if (options.ErrorMessage is not null && options.ErrorContent is not null)
        {
            throw new InvalidOperationException("Set either MediatorMcpOptions.ErrorMessage or ErrorContent, not both.");
        }

        builder.Services.AddSingleton<IConfigureOptions<McpServerOptions>>(
            provider => new ActionToolsSetup(provider.GetRequiredService<IActionCatalog>(), options));

        return builder;
    }
}
