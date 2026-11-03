using System;
using System.Linq;
using Datapoint.Mediator;
using Datapoint.Mediator.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace System.CommandLine;

/// <summary>
/// Extension methods for adding mediator actions as CLI commands.
/// </summary>
public static class MediatorCommandExtensions
{
    /// <summary>
    /// Adds every action in <see cref="IActionCatalog"/> as a subcommand of <paramref name="command"/>.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command, such as <see cref="RootCommand"/>.</typeparam>
    /// <param name="command">The command to add the actions to.</param>
    /// <param name="services">The app's service provider, which the mediator is registered in.</param>
    /// <param name="configure">Configures which actions are added and how, or <see langword="null"/>.</param>
    /// <returns>The command, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each dot in an action's name starts a subcommand, so <c>invoices.create</c> becomes <c>invoices create</c>.
    /// Each public property of the action becomes a typed option, such as <c>--customer</c>, which is required unless
    /// it's nullable or has a default value, and is described by a <see cref="ComponentModel.DescriptionAttribute"/>
    /// on the property or record parameter. A <c>--json</c> option accepts all the arguments as JSON instead, for
    /// actions with nested objects or collections.
    /// </para>
    /// <para>
    /// Properties marked with <see cref="HostProvidedAttribute"/> get no option, and <c>--json</c> arguments that contain
    /// one, in any letter case, are refused with <see cref="ActionExitCodes.InvalidArguments"/>. The host must set their
    /// values itself, for example in a decorator of <see cref="IActionDispatcher"/>.
    /// </para>
    /// <para>
    /// Each invocation runs in its own DI scope, and Ctrl+C cancels it. Results are written to the invocation's output
    /// as JSON; a stream query writes one JSON value per line as results arrive. Errors are written to the error output,
    /// with the exit codes in <see cref="ActionExitCodes"/>. Unexpected errors print no details.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="command"/> or <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static TCommand AddMediatorActions<TCommand>(
        this TCommand command,
        IServiceProvider services,
        Action<MediatorCommandLineOptions>? configure = null)
        where TCommand : Command
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(services);

        var options = new MediatorCommandLineOptions();
        configure?.Invoke(options);

        foreach (var action in services.GetRequiredService<IActionCatalog>().Actions)
        {
            if (!(options.Filter?.Invoke(action) ?? true))
            {
                continue;
            }

            var segments = action.Name.Split('.');
            Command parent = command;

            foreach (var group in segments[..^1])
            {
                var existing = parent.Subcommands.FirstOrDefault(c => c.Name == group);

                if (existing is null)
                {
                    existing = new Command(group);
                    parent.Subcommands.Add(existing);
                }

                parent = existing;
            }

            parent.Subcommands.Add(ActionCommand.Create(segments[^1], action, services, options));
        }

        return command;
    }
}
