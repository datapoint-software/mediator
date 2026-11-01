# ASP.NET Core

`Datapoint.Mediator.AspNetCore` maps every [action](../actions.md) as an HTTP endpoint, with OpenAPI metadata and safe error responses.

```bash
dotnet add package Datapoint.Mediator.AspNetCore --prerelease
```

```csharp
builder.Services.AddInvoicing();                   // your AddMediator(...) call
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddHttpPrincipalAccessor();       // from Datapoint.Mediator.Authorization.AspNetCore
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapActions("/api").RequireAuthorization();
app.Run();
```

The full app is in [`samples/Invoicing/Invoicing.Api`](../../samples/Invoicing/Invoicing.Api/Program.cs).

## Endpoints

Each action becomes a `POST` endpoint at its name, with dots turned into slashes: `invoices.create` is served at `POST /api/invoices/create`.

| Request | |
|---|---|
| Body | the action's JSON, such as `{"customer":"Ana","amount":100}`. An empty body means no arguments. |
| Cancellation | the request's `RequestAborted` token |
| DI scope | the request's scope |

| Action kind | Response |
|---|---|
| Command without a result | `204 No Content` |
| Command with a result, query | `200` with the result as JSON |
| Stream query | `200` with a JSON array, streamed as results arrive |

For stream queries, the first result is requested before the response starts, so authorization and validation failures still return a proper status code rather than a broken `200`.

Every action uses `POST`, including queries, because their arguments are a JSON body, and because it keeps them out of URLs, logs and browser history.

## Conventions and OpenAPI

`MapActions` returns a `RouteGroupBuilder`, so ASP.NET Core conventions apply to every action endpoint:

```csharp
app.MapActions("/api")
    .RequireAuthorization()
    .RequireRateLimiting("api")
    .RequireCors("frontend");
```

Each endpoint carries its name (the action's name), its summary (the action's description), a tag (the first segment of the name, such as `invoices`), its request type, and its response types, including `400`, `401` and `403` problem responses. With `builder.Services.AddOpenApi()` and `app.MapOpenApi()`, every action is documented at `/openapi/v1.json`.

## Options

```csharp
app.MapActions("/api", o =>
{
    o.Filter = a => a.Name.StartsWith("invoices.");   // map only some actions
    o.SerializerOptions = mySerializerOptions;          // default: the app's JsonOptions
    o.MapException = ...;                               // see Errors
});
```

You can call `MapActions` several times with different prefixes and filters, for example to require different policies for different groups.

## Errors

| Failure | Response |
|---|---|
| The body isn't valid JSON, or doesn't fit the action's type | `400`, with a fixed message |
| No authenticated user | `401` |
| The user may not run the action | `403` |
| Anything else | `MapException`, if it handles it; otherwise rethrown to the app's exception handling |

Responses are `ProblemDetails`, and never repeat the request's input. Map your own exceptions with `MapException`. Return `null` for exceptions it doesn't handle:

```csharp
app.MapActions("/api", o => o.MapException = e => e switch
{
    ValidationException v => Results.ValidationProblem(v.Errors
        .GroupBy(f => f.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray())),   // never AttemptedValue
    InvoiceNotFoundException => Results.Problem(statusCode: 404, title: "The invoice doesn't exist."),
    _ => null,
});
```

Unhandled exceptions reach ASP.NET Core's exception handling. In production, use `app.UseExceptionHandler()` with `builder.Services.AddProblemDetails()`, so they become a `500` without details.

## The current user

`builder.Services.AddHttpPrincipalAccessor()`, from `Datapoint.Mediator.Authorization.AspNetCore`, makes actions run as the request's user. Call `app.UseAuthentication()` before mapping the actions. See [Authorization](../authorization.md#the-current-user).

`RequireAuthorization()` on the group refuses anonymous requests before they reach the mediator. The mediator's own authorizers then decide per action. Using both is defence in depth.

## Coexisting with your own endpoints

Action endpoints are ordinary minimal API endpoints. Keep your own endpoints for anything that isn't a good fit, such as file uploads, webhooks with a fixed payload, or `GET` URLs that must be cacheable, and send commands from them through `IMediator`.
