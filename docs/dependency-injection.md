# Dependency injection

`Datapoint.Mediator.DependencyInjection` registers the mediator, your handlers and behaviours with `Microsoft.Extensions.DependencyInjection`.

```csharp
services.AddMediator(o => o
    .AddHandlersFromAssemblyContaining<CreateInvoiceHandler>()
    .AddAuthorizationBehavior()
    .AddValidationBehavior()
    .AddTransactionBehavior<InvoicingDbContext>());
```

## What `AddMediator` registers

| Service | Lifetime | Notes |
|---|---|---|
| `IMediator` | scoped | Resolves handlers and behaviours from the scope it was resolved from. |
| `IActionCatalog` | singleton | The named [actions](actions.md). |
| `IActionDispatcher` | scoped | Dispatches actions by name. |
| Handlers | scoped, by default | One per command or query; any number per message. |
| Behaviours | scoped, by default | In the order they're added. |

Always resolve `IMediator` from a scope: the request's scope in ASP.NET Core, or one you create with `CreateAsyncScope()` in a worker or console app. Handlers then share that scope's services, such as one `DbContext`.

## Registering handlers

Scan an assembly:

```csharp
o.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>();
o.AddHandlersFromAssembly(typeof(CreateInvoiceHandler).Assembly);
```

- Every concrete, non-generic class implementing a handler interface is registered, including `internal` ones.
- Message handlers are registered in order of their full type name, so the order they run in is predictable.
- Scanning never registers behaviours, because their order matters. Add them explicitly.

Or add handlers one by one:

```csharp
o.AddHandler<CreateInvoiceHandler>();
```

A handler class can handle several requests by implementing several interfaces.

## Lifetimes

Handlers and behaviours are **scoped** by default, which is right for most apps: they can depend on scoped services, such as a `DbContext` or the current user. Change the default, or set a lifetime per registration:

```csharp
o.DefaultLifetime = ServiceLifetime.Transient;
o.AddHandler<ExchangeRatesHandler>(ServiceLifetime.Singleton);
```

> **Captive dependency:** a singleton must never depend on a scoped service, because it would keep the first scope's instance forever. With `ValidateScopes` turned on, which is the default in ASP.NET Core development, the container reports this at startup.

The built-in behaviours (authorization, validation, transactions) are always scoped, whatever `DefaultLifetime` says, because they depend on scoped services.

## Errors found at startup

`AddMediator` throws `InvalidOperationException` when the configuration can't work, so mistakes are found when the app starts, not at the first request:

- a second handler for the same command or query;
- an invalid or duplicate [action name](actions.md#names), or a message marked as an action.

## Calling `AddMediator` more than once

Several modules can each call `AddMediator`. Registering the same handler or behaviour again has no effect, and every module's actions end up in the same catalog. Behaviours run in the order they were first added, across all calls.
