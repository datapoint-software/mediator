using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datapoint.Mediator;
using Datapoint.Mediator.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for mapping mediator actions as HTTP endpoints.
/// </summary>
public static class ActionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps every action in <see cref="IActionCatalog"/> as a <c>POST</c> endpoint under <paramref name="prefix"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">The route prefix, such as <c>/api</c>.</param>
    /// <param name="configure">Configures which actions are mapped and how, or <see langword="null"/>.</param>
    /// <returns>A group builder for the endpoints, so conventions such as <c>RequireAuthorization</c> apply to all of them.</returns>
    /// <remarks>
    /// <para>
    /// Each action is mapped at its name with dots replaced by slashes, so <c>invoices.create</c> is served at
    /// <c>POST {prefix}/invoices/create</c>. The request body is the action's JSON, and an empty body is treated as an
    /// empty object. A command without a result returns <c>204 No Content</c>; other actions return <c>200 OK</c> with
    /// their result as JSON, and stream queries stream a JSON array.
    /// </para>
    /// <para>
    /// Requests are dispatched through <see cref="IActionDispatcher"/>, so every behaviour runs, including authorization.
    /// Invalid arguments return <c>400</c>, missing authentication <c>401</c> and denied access <c>403</c>, as
    /// <c>ProblemDetails</c>. Other exceptions go to <see cref="MediatorEndpointOptions.MapException"/>, and are
    /// otherwise left to the app's exception handling.
    /// </para>
    /// <para>
    /// Each endpoint is named after its action, tagged with its name's first segment, and describes its request and
    /// response types, for OpenAPI documents.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> or <paramref name="prefix"/> is <see langword="null"/>.
    /// </exception>
    public static RouteGroupBuilder MapActions(
        this IEndpointRouteBuilder endpoints,
        string prefix = "",
        Action<MediatorEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);

        var options = new MediatorEndpointOptions();
        configure?.Invoke(options);

        var group = endpoints.MapGroup(prefix);

        foreach (var action in endpoints.ServiceProvider.GetRequiredService<IActionCatalog>().Actions)
        {
            if (options.Filter?.Invoke(action) ?? true)
            {
                Map(group, action, options);
            }
        }

        return group;
    }

    private static void Map(RouteGroupBuilder group, ActionDescriptor action, MediatorEndpointOptions options)
    {
        var endpoints = new ActionEndpoints(action, options);
        var endpoint = group
            // A route handler delegate, not a RequestDelegate, so the returned IResult is written to the response.
            .MapPost("/" + action.Name.Replace('.', '/'), (Func<HttpContext, Task<IResult>>)endpoints.HandleAsync)
            .WithName(action.Name)
            .WithTags(action.Name.Split('.')[0])
            .Accepts(action.ActionType, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        if (action.Description is { } description)
        {
            endpoint.WithSummary(description);
        }

        _ = action.Kind switch
        {
            ActionKind.Command => endpoint.Produces(StatusCodes.Status204NoContent),
            ActionKind.StreamQuery => endpoint.Produces(StatusCodes.Status200OK, typeof(IEnumerable<>).MakeGenericType(action.ResultType!)),
            _ => endpoint.Produces(StatusCodes.Status200OK, action.ResultType),
        };
    }
}
