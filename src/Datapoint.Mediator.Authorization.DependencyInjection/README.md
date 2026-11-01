# Datapoint.Mediator.Authorization.DependencyInjection

Registers the [Datapoint.Mediator.Authorization](https://www.nuget.org/packages/Datapoint.Mediator.Authorization) behaviour and your authorizers.

```csharp
services.AddFixedPrincipalAccessor(principal);   // or AddHttpPrincipalAccessor() in an API: one per host
services.AddAuthorizersFromAssemblyContaining<DeleteInvoiceAuthorizer>();
services.AddMediator(options =>
{
    options.AddHandlersFromAssemblyContaining<DeleteInvoiceHandler>();
    options.AddAuthorizationBehavior();   // before validation
    options.AddValidationBehavior();
});
```

- The behaviour is always registered as scoped, so it resolves the current user and authorizers from the current scope.
- `AddAuthorizersFromAssembly` registers every concrete authorizer class, as scoped by default. An action can have several authorizers, and all of them must allow it.
- `AddFixedPrincipalAccessor` registers the same user, or `null` when no one is signed in, for every command and query: for CLIs and MCP servers over stdio. Registering a second `IPrincipalAccessor` throws `InvalidOperationException`.
