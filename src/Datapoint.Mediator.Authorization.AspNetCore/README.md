# Datapoint.Mediator.Authorization.AspNetCore

Provides the ASP.NET Core request's user to the [Datapoint.Mediator.Authorization](https://www.nuget.org/packages/Datapoint.Mediator.Authorization) behaviour, for APIs and MCP servers over HTTP.

```csharp
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddHttpPrincipalAccessor();

var app = builder.Build();
app.UseAuthentication();   // sets the request's user, before any endpoint runs
app.MapActions("/api");    // from Datapoint.Mediator.AspNetCore
```

An anonymous request has no authenticated user, so protected actions fail with `AuthenticationRequiredException` (HTTP 401). Outside a request, such as in a background service, there is no user.

**Documentation:** [Authorization](https://github.com/datapoint-software/mediator/blob/main/docs/authorization.md#the-current-user) · [Background jobs](https://github.com/datapoint-software/mediator/blob/main/docs/use-cases.md#background-jobs)
