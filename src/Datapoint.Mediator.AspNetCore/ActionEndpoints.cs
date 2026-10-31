using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Datapoint.Mediator.AspNetCore;

// Handles a request for one action: reads the JSON body, dispatches the action by name, and writes the result.
internal sealed class ActionEndpoints(ActionDescriptor action, MediatorEndpointOptions options)
{
    public async Task<IResult> HandleAsync(HttpContext context)
    {
        var services = context.RequestServices;
        var serializerOptions = options.SerializerOptions
            ?? services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions
            ?? JsonSerializerOptions.Web;
        var ct = context.RequestAborted;

        // The body is read first, because an empty body (treated as no arguments) can't always be recognised from
        // the headers: chunked requests have no Content-Length.
        JsonElement arguments = default;
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, ct).ConfigureAwait(false);

        if (body.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(body.GetBuffer().AsMemory(0, (int)body.Length));
                arguments = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Results.Problem(
                    title: "Invalid request body",
                    detail: "The request body is not valid JSON.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var dispatcher = services.GetRequiredService<IActionDispatcher>();

        try
        {
            if (action.Kind == ActionKind.StreamQuery)
            {
                return await StreamAsync(dispatcher, arguments, serializerOptions, ct).ConfigureAwait(false);
            }

            var result = await dispatcher.DispatchAsync(action.Name, arguments, serializerOptions, ct).ConfigureAwait(false);

            return action.Kind == ActionKind.Command ? Results.NoContent() : Results.Json(result, serializerOptions);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && Map(exception) is { } response)
        {
            return response;
        }
    }

    // Authorization and validation run when a stream's first result is requested, so it's requested here, before the
    // response starts: a failure can then still become a 400, 401 or 403 instead of a broken 200.
    private async Task<IResult> StreamAsync(
        IActionDispatcher dispatcher,
        JsonElement arguments,
        JsonSerializerOptions serializerOptions,
        CancellationToken ct)
    {
        var enumerator = dispatcher.StreamAsync(action.Name, arguments, serializerOptions, ct).GetAsyncEnumerator(ct);
        bool hasFirst;

        try
        {
            hasFirst = await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return Results.Json(Remaining(enumerator, hasFirst), serializerOptions);
    }

    private static async IAsyncEnumerable<object?> Remaining(IAsyncEnumerator<object?> enumerator, bool hasFirst)
    {
        await using (enumerator.ConfigureAwait(false))
        {
            if (!hasFirst)
            {
                yield break;
            }

            yield return enumerator.Current;

            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return enumerator.Current;
            }
        }
    }

    // Our own exceptions get fixed, safe responses. Anything else goes to the app's hook, and is otherwise rethrown
    // for the app's exception handling.
    private IResult? Map(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException => Results.Problem(
                title: "Invalid arguments",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest),
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AuthenticationRequiredException" =>
                Results.Problem(title: "Authentication is required.", statusCode: StatusCodes.Status401Unauthorized),
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AccessDeniedException" =>
                Results.Problem(title: "Access is denied.", statusCode: StatusCodes.Status403Forbidden),
            _ => options.MapException?.Invoke(exception),
        };
}
