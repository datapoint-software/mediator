using System;

namespace Datapoint.Mediator;

/// <summary>
/// The base class of the attributes that say which host adapters offer an action, such as <see cref="ApiAttribute"/>,
/// <see cref="CommandLineAttribute"/> and <see cref="ModelContextProtocolAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every action needs at least one: <see cref="IActionCatalog"/> refuses an action without any at startup. An adapter
/// offers only the actions marked with its own attribute, and an action it doesn't offer fails there like an unknown
/// one.
/// </para>
/// <para>
/// A custom adapter derives its own sealed attribute from this class, and checks it with
/// <see cref="ActionDescriptor.IsOfferedBy{TChannel}"/> before offering or dispatching an action:
/// </para>
/// <code>
/// public sealed class GrpcAttribute : ActionChannelAttribute;
///
/// foreach (var action in catalog.Actions.Where(action => action.IsOfferedBy&lt;GrpcAttribute&gt;()))
/// {
///     // Offer the action.
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public abstract class ActionChannelAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionChannelAttribute"/> class.
    /// </summary>
    protected ActionChannelAttribute()
    {
    }
}
