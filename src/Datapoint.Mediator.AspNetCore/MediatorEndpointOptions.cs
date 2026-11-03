using System;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Datapoint.Mediator.AspNetCore;

/// <summary>
/// Configures how actions are mapped as HTTP endpoints.
/// </summary>
public sealed class MediatorEndpointOptions
{
    /// <summary>
    /// Gets or sets which actions to map, or <see langword="null"/> to map every action marked with
    /// <see cref="ApiAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The filter only sees actions marked with <see cref="ApiAttribute"/>, so it can narrow them but never add others.
    /// </remarks>
    public Func<ActionDescriptor, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the options used for request bodies and responses, or <see langword="null"/> to use the app's
    /// <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into a response, or returns
    /// <see langword="null"/> to keep Mediator's response for invalid arguments, or to let the app's exception handling
    /// deal with any other exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The response is sent to the caller, so it must never contain secrets, personal data or internal details. Write
    /// your own text: an exception's message is for logs, including your own exceptions'. For example, to report
    /// FluentValidation failures, return <c>Results.ValidationProblem</c> with each failure's property name and error
    /// message, and never its attempted value.
    /// </para>
    /// <para>
    /// The function also receives <see cref="InvalidActionArgumentsException"/>, before Mediator writes its own
    /// <c>400</c>, so you can report it with your own codes, from its <c>ActionName</c> and <c>Field</c>. Never return its
    /// message or its inner exception's message, which can contain .NET type names and property names the caller chose.
    /// Authentication and access-denied responses can't be replaced here; add fields to them with
    /// <c>ProblemDetailsOptions.CustomizeProblemDetails</c>.
    /// </para>
    /// <para>
    /// The responses Mediator writes itself never contain argument values, .NET type names, stack traces or paths.
    /// Their <c>title</c> and <c>detail</c> are meant for developers and agents, not for display in a user interface.
    /// </para>
    /// </remarks>
    public Func<Exception, IResult?>? MapException { get; set; }
}
