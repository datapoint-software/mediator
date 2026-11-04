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
        var cancellationToken = context.RequestAborted;

        // The body is read first, because an empty body (treated as no arguments) can't always be recognised from
        // the headers: chunked requests have no Content-Length.
        JsonElement arguments = default;
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, cancellationToken).ConfigureAwait(false);

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
                return await StreamAsync(dispatcher, arguments, serializerOptions, cancellationToken).ConfigureAwait(false);
            }

            var result = await dispatcher.DispatchAsync(action.Name, arguments, serializerOptions, cancellationToken).ConfigureAwait(false);

            return action.Kind == ActionKind.Command ? Results.NoContent() : Results.Json(result, serializerOptions);
        }
        catch (Exception exception) when (!AdapterErrors.IsCallerCancellation(exception, cancellationToken))
        {
            // Mapped in the catch block rather than in the filter, so the hook runs after the action's finally blocks,
            // and an exception it throws isn't silently swallowed by the filter.
            if (Map(exception) is { } response)
            {
                return response;
            }

            throw;
        }
    }

    // Authorization and validation run when a stream's first result is requested, so it's requested here, before the
    // response starts: a failure can then still become a 400, 401 or 403 instead of a broken 200.
    private async Task<IResult> StreamAsync(
        IActionDispatcher dispatcher,
        JsonElement arguments,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken)
    {
        var enumerator = dispatcher.StreamAsync(action.Name, arguments, serializerOptions, cancellationToken).GetAsyncEnumerator(cancellationToken);
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

    // Our own exceptions get fixed, safe responses. Invalid arguments go to the app's hook first, so it can report them
    // with its own codes. Anything else goes to the hook, and is otherwise rethrown for the app's exception handling.
    private IResult? Map(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException invalid => CallMapException(exception) ?? InvalidArguments(invalid),
            _ when AdapterErrors.IsAuthenticationRequired(exception) =>
                Results.Problem(title: AdapterErrors.AuthenticationRequired, statusCode: StatusCodes.Status401Unauthorized),
            _ when AdapterErrors.IsAccessDenied(exception) =>
                Results.Problem(title: AdapterErrors.AccessDenied, statusCode: StatusCodes.Status403Forbidden),
            _ => CallMapException(exception),
        };

    // An exception from the app's hook is treated as if the hook had returned null, because nothing about it is safe to
    // send the caller: Mediator's own response is sent, or the action's exception is rethrown.
    private IResult? CallMapException(Exception exception)
    {
        try
        {
            return options.MapException?.Invoke(exception);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Built from the field and the reason only, never from the exception's message.
    private IResult InvalidArguments(InvalidActionArgumentsException exception)
    {
        if (AdapterErrors.SafeField(exception) is not { } field)
        {
            return Results.Problem(
                title: "Invalid arguments",
                detail: $"The arguments for action '{action.Name}' are not valid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var error = AdapterErrors.IsRequired(exception) ? "The argument is required." : "The argument is not valid.";
        return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [error] }, title: "Invalid arguments");
    }
}
