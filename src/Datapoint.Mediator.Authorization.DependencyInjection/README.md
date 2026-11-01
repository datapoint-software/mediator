# Datapoint.Mediator.Authorization.DependencyInjection

Registers the [Datapoint.Mediator.Authorization](https://www.nuget.org/packages/Datapoint.Mediator.Authorization) behaviour, your authorizers, and the current user for hosts that act for one user, such as a CLI.

```csharp
services.AddAuthorizersFromAssemblyContaining<CreateInvoiceAuthorizer>();
services.AddFixedPrincipalAccessor(signedInUser);   // CLI or MCP over stdio; null when nobody is signed in
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()                     // before validation
    .AddValidationBehavior());
```

Registering a second, different `IPrincipalAccessor` throws, so a misconfiguration can't silently change the current user.

**Documentation:** [Authorization](https://github.com/datapoint-software/mediator/blob/main/docs/authorization.md)
