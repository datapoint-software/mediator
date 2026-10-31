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
    /// Gets or sets which actions to map, or <see langword="null"/> to map every action in the catalog.
    /// </summary>
    public Func<ActionDescriptor, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the options used for request bodies and responses, or <see langword="null"/> to use the app's
    /// <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a function that turns an exception thrown by an action into a response, or returns
    /// <see langword="null"/> to let the app's exception handling deal with it.
    /// </summary>
    /// <remarks>
    /// The response is sent to the caller, so it must never contain secrets, personal data or internal details. For
    /// example, to report FluentValidation failures, return <c>Results.ValidationProblem</c> with each failure's property
    /// name and error message, and never its attempted value.
    /// </remarks>
    public Func<Exception, IResult?>? MapException { get; set; }
}
