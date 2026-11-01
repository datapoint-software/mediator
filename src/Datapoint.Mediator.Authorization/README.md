# Datapoint.Mediator.Authorization

A [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) behaviour that authorizes commands, queries and stream queries for the current user, **denying access by default**. The rules live in your business layer, so they apply the same way from an API, a CLI or an MCP server. Register it with [Datapoint.Mediator.Authorization.DependencyInjection](https://www.nuget.org/packages/Datapoint.Mediator.Authorization.DependencyInjection).

```csharp
public sealed class CreateInvoiceAuthorizer : IAuthorizer<CreateInvoice>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(CreateInvoice action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new(AuthorizationDecision.AllowIf(principal.IsInRole("Finance")));
}
```

A decision can carry details of your own, such as the share or rule that applied, for your handlers (`IAuthorizationContext`) and audit log (`IAuthorizationObserver`); they never reach the caller. An action with no authorizer is denied, unless it's marked `[AllowAnonymousAccess]`. With no signed-in user, `AuthenticationRequiredException` is thrown.

> **Security:** messages aren't authorized. Never publish a message built from user input: send an authorized command, and publish the message from its handler.

**Documentation:** [Authorization](https://github.com/datapoint-software/mediator/blob/main/docs/authorization.md) · [Security checklist](https://github.com/datapoint-software/mediator/blob/main/docs/security.md)
