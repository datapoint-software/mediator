using System;

namespace Datapoint.Mediator;

/// <summary>
/// Marks a property of an action as supplied by the host, never by the caller, such as the tenant of the caller's
/// session. Hosts hide it from callers, and refuse a value a caller gives for it.
/// </summary>
/// <remarks>
/// <para>
/// Put it on a property, or on a positional record's parameter. Only the action's own top-level properties count,
/// not properties of nested objects.
/// </para>
/// <para>
/// The command line adapter adds no option for the property, and refuses <c>--json</c> arguments that contain it. The
/// MCP adapter leaves it out of the tool's input schema, and refuses tool calls that contain it. Both match its JSON
/// name ignoring case, as <see cref="System.Text.Json"/> does when binding.
/// </para>
/// <para>
/// <b>Hiding is presentation only, not security.</b> <see cref="IActionDispatcher"/>, <see cref="IMediator"/> and the
/// ASP.NET Core adapter accept the property like any other. The host must set or overwrite the value before the action
/// is dispatched, for example in middleware or in a decorator of <see cref="IActionDispatcher"/>, and must never trust
/// a value that came from the caller. A host that doesn't set it dispatches the property's default value.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class HostProvidedAttribute : Attribute
{
    /// <summary>
    /// Gets why, or from where, the host supplies the value, for documentation, such as "The tenant of the caller's
    /// session.".
    /// </summary>
    public string? Reason { get; init; }
}
