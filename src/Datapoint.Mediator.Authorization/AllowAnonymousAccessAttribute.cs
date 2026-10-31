using System;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Marks a command, query or stream query that anyone may run, whether or not they are signed in.
/// </summary>
/// <remarks>
/// The attribute is not inherited, so a type derived from an anonymous action must be marked again to be anonymous.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AllowAnonymousAccessAttribute : Attribute;
