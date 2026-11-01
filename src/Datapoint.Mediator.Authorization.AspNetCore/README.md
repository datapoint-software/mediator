# Datapoint.Mediator.Authorization.AspNetCore

Provides the ASP.NET Core request's user to the [Datapoint.Mediator.Authorization](https://www.nuget.org/packages/Datapoint.Mediator.Authorization) behaviour, so commands and queries sent from an API, or from an MCP server over HTTP, are authorized for the user who made the request.

```csharp
builder.Services.AddAuthentication(/* your scheme, such as JWT bearer */);
builder.Services.AddHttpPrincipalAccessor();

var app = builder.Build();
app.UseAuthentication();   // sets the request's user, before any endpoint runs
app.MapActions("/api");    // from Datapoint.Mediator.AspNetCore
```

- `AddHttpPrincipalAccessor` registers `HttpPrincipalAccessor` as the scoped `IPrincipalAccessor`, along with `IHttpContextAccessor`.
- An anonymous request has a user without an authenticated identity, so actions that need a user fail with `AuthenticationRequiredException`. `MapActions` turns that into HTTP 401.
- Outside a request, such as in a background service, there is no user.
- Only one `IPrincipalAccessor` can be registered: registering another one throws `InvalidOperationException`, so a misconfiguration can't silently change who the current user is.
