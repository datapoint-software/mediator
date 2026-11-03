using System;

namespace Datapoint.Mediator;

/// <summary>
/// Offers an action as a CLI command, through <c>AddMediatorActions</c> in Datapoint.Mediator.CommandLine.
/// </summary>
/// <remarks>
/// An action without this attribute isn't offered there, and fails like an unknown action. See
/// <see cref="ActionChannelAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CommandLineAttribute : ActionChannelAttribute
{
}
