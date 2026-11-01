# Testing

Handlers, validators and authorizers are plain classes, so most tests don't need the mediator at all.

## Handlers

Create the handler with its dependencies, and call it directly:

```csharp
[Fact]
public async Task CreateInvoice_AddsAnOpenInvoice()
{
    await using var db = CreateDbContext();   // for example SQLite in memory
    var handler = new CreateInvoiceHandler(db, new FixedPrincipalAccessor(User("ana")));

    var id = await handler.HandleAsync(new CreateInvoice("Ana", 100m));

    var invoice = db.Invoices.Local.Single();
    Assert.Equal(id, invoice.Id);
    Assert.Equal(InvoiceStatus.Open, invoice.Status);
}
```

`FixedPrincipalAccessor` from `Datapoint.Mediator.Authorization` is handy wherever a handler needs the current user.

Handlers that rely on the transaction behaviour don't save, so check the change tracker (`db.Invoices.Local`), or call `SaveChangesAsync` in the test.

## Validators

```csharp
[Fact]
public async Task CreateInvoice_WithoutCustomer_IsInvalid()
{
    var result = await new CreateInvoiceValidator().ValidateAsync(new CreateInvoice("", 100m));

    Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateInvoice.Customer));
}
```

## Authorizers

```csharp
[Fact]
public async Task CreateInvoice_RequiresFinanceRole()
{
    var authorizer = new InvoiceAuthorizer();

    Assert.True(await authorizer.IsAuthorizedAsync(new CreateInvoice("Ana", 1m), User("ana", "Finance")));
    Assert.False(await authorizer.IsAuthorizedAsync(new CreateInvoice("Ana", 1m), User("rui")));
}

static ClaimsPrincipal User(string name, params string[] roles) =>
    new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, name), .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
        authenticationType: "test"));
```

Give test identities an authentication type, such as `"test"`. An identity without one is unauthenticated, and the authorization behaviour treats it as no user.

## The whole pipeline

To check that everything fits together, with registration, behaviours, authorization and validation, build a real service provider with your production registration:

```csharp
[Fact]
public async Task CreateInvoice_WithoutFinanceRole_IsDenied()
{
    await using var services = new ServiceCollection()
        .AddInvoicing()
        .AddFixedPrincipalAccessor(User("rui"))
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    await using var scope = services.CreateAsyncScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    await Assert.ThrowsAsync<AccessDeniedException>(
        async () => await mediator.SendAsync(new CreateInvoice("Ana", 100m)));
}
```

`ValidateScopes` and `ValidateOnBuild` catch lifetime mistakes, such as a singleton depending on a scoped service, when the provider is built.

## Every action has an authorizer

Authorization fails closed, so an action without an authorizer is denied. A test can catch a forgotten one before users do:

```csharp
[Fact]
public void EveryAction_HasAnAuthorizerOrAllowsAnonymousAccess()
{
    using var services = new ServiceCollection().AddInvoicing().BuildServiceProvider();
    var catalog = services.GetRequiredService<IActionCatalog>();

    var missing = catalog.Actions
        .Where(a => !a.ActionType.IsDefined(typeof(AllowAnonymousAccessAttribute), inherit: false))
        .Where(a => !services.GetServices(typeof(IAuthorizer<>).MakeGenericType(a.ActionType)).Any())
        .Select(a => a.Name);

    Assert.Empty(missing);
}
```

## Hosts

- **ASP.NET Core:** use `WebApplicationFactory` or `Microsoft.AspNetCore.TestHost`, with a test authentication handler, and call the action endpoints with `HttpClient`.
- **CLI:** invoke the root command with an `InvocationConfiguration` whose `Output` and `Error` are `StringWriter`s, and check the exit code and output.
- **MCP:** connect a client from the `ModelContextProtocol` SDK to the server over an in-memory transport, and call `tools/list` and `tools/call`.

The library's own tests do all three, in `tests/Datapoint.Mediator.AspNetCore.Tests`, `tests/Datapoint.Mediator.CommandLine.Tests` and `tests/Datapoint.Mediator.ModelContextProtocol.Tests`.
