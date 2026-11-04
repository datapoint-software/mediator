using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Datapoint.Mediator;

/// <summary>
/// The actions, among a set of command, query and stream query types, that are marked with
/// <see cref="ActionAttribute"/>.
/// </summary>
public sealed partial class ActionCatalog : IActionCatalog
{
    private const int MaxNameLength = 64;

    private readonly FrozenDictionary<string, ActionDescriptor> _actions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCatalog"/> class from the action types that have a handler.
    /// </summary>
    /// <param name="actionTypes">
    /// The command, query, stream query and message types that have a handler. Types without
    /// <see cref="ActionAttribute"/> are ignored.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="actionTypes"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// An action's name is not valid or is used twice, an action's description is empty, an action has no
    /// <see cref="ActionChannelAttribute"/>, a message is marked as an action, or a type implements more than one
    /// action interface.
    /// </exception>
    public ActionCatalog(IEnumerable<Type> actionTypes)
    {
        ArgumentNullException.ThrowIfNull(actionTypes);

        var named = new Dictionary<string, Type>(StringComparer.Ordinal);
        var actions = new List<ActionDescriptor>();
        var unoffered = new List<Type>();

        foreach (var type in actionTypes.Distinct())
        {
            var attribute = type.GetCustomAttribute<ActionAttribute>(inherit: false);

            if (attribute is null)
            {
                continue;
            }

            Validate(type, attribute);

            if (named.TryGetValue(attribute.Name, out var other))
            {
                throw new InvalidOperationException(
                    $"'{type}' and '{other}' are both named '{attribute.Name}'. Action names must be unique.");
            }

            named.Add(attribute.Name, type);
            var (kind, resultType) = GetKind(type);
            actions.Add(new ActionDescriptor(attribute.Name, attribute.Description, kind, type, resultType));

            if (!type.IsDefined(typeof(ActionChannelAttribute), inherit: false))
            {
                unoffered.Add(type);
            }
        }

        EnsureOffered(unoffered);

        _actions = actions.ToFrozenDictionary(action => action.Name, StringComparer.Ordinal);
        Actions = [.. actions.OrderBy(action => action.Name, StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<ActionDescriptor> Actions { get; }

    /// <inheritdoc/>
    public bool TryGetAction(string name, [NotNullWhen(true)] out ActionDescriptor? action)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _actions.TryGetValue(name, out action);
    }

    private static void Validate(Type type, ActionAttribute attribute)
    {
        if (typeof(IMessage).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"'{type}' is a message, so it can't be an action: messages are published without authorization.");
        }

        if (attribute.Name.Length > MaxNameLength || !NamePattern().IsMatch(attribute.Name))
        {
            throw new InvalidOperationException(
                $"'{attribute.Name}' on '{type}' is not a valid action name. Use up to {MaxNameLength} lowercase " +
                "letters and digits, separated by single dots or hyphens, such as 'invoices.create'.");
        }

        if (string.IsNullOrWhiteSpace(attribute.Description))
        {
            throw new InvalidOperationException(
                $"'{attribute.Name}' on '{type}' has no description. Describe what the action does, because hosts " +
                "show it to callers as the OpenAPI summary, in CLI help and as the MCP tool's description.");
        }
    }

    // Listed together, so every action can be fixed in one pass.
    private static void EnsureOffered(List<Type> unoffered)
    {
        if (unoffered.Count > 0)
        {
            throw new InvalidOperationException(
                $"No host adapter offers {string.Join(", ", unoffered.Select(type => $"'{type}'"))}. Mark each action " +
                "with the adapters that offer it, such as [Api], [CommandLine] or [ModelContextProtocol]. Code that " +
                "only your application runs doesn't need [Action]: send it through IMediator.");
        }
    }

    private static (ActionKind Kind, Type? ResultType) GetKind(Type type)
    {
        var kinds = new List<(ActionKind, Type?)>();

        foreach (var @interface in type.GetInterfaces())
        {
            if (@interface == typeof(ICommand))
            {
                kinds.Add((ActionKind.Command, null));
            }
            else if (@interface.IsGenericType)
            {
                var definition = @interface.GetGenericTypeDefinition();
                var resultType = @interface.GetGenericArguments()[0];

                if (definition == typeof(ICommand<>))
                {
                    kinds.Add((ActionKind.CommandWithResult, resultType));
                }
                else if (definition == typeof(IQuery<>))
                {
                    kinds.Add((ActionKind.Query, resultType));
                }
                else if (definition == typeof(IStreamQuery<>))
                {
                    kinds.Add((ActionKind.StreamQuery, resultType));
                }
            }
        }

        return kinds.Count == 1
            ? kinds[0]
            : throw new InvalidOperationException(
                $"'{type}' must implement exactly one of ICommand, ICommand<TResult>, IQuery<TResult> or " +
                "IStreamQuery<TResult> to be an action, so it can be dispatched by name unambiguously.");
    }

    [GeneratedRegex("^[a-z0-9]+([.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
