using System;

namespace Datapoint.Mediator;

/// <summary>
/// Offers an action as an MCP tool, through <c>WithMediatorActions</c> in Datapoint.Mediator.ModelContextProtocol.
/// </summary>
/// <remarks>
/// An action without this attribute isn't offered there, and fails like an unknown action. See
/// <see cref="ActionChannelAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModelContextProtocolAttribute : ActionChannelAttribute
{
}
