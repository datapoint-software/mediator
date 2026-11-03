using System;

namespace Datapoint.Mediator;

/// <summary>
/// Offers an action as an HTTP endpoint, through <c>MapMediatorActions</c> in Datapoint.Mediator.AspNetCore.
/// </summary>
/// <remarks>
/// An action without this attribute isn't offered there, and fails like an unknown action. See
/// <see cref="ActionChannelAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ApiAttribute : ActionChannelAttribute
{
}
