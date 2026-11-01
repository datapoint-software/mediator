# Datapoint.Mediator.CommandLine

Adds [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/) commands, so a CLI offers the same commands and queries as an API or an MCP server, with authorization, validation, transactions and telemetry applied.

```csharp
[Action("invoices.create", Description = "Creates an invoice for a customer.")]
public sealed record CreateInvoice(
    [property: Description("The customer's name.")] string Customer,
    decimal Amount,
    string? Notes = null) : ICommand<Guid>;

var services = new ServiceCollection()
    .AddMediator(o => o.AddHandlersFromAssemblyContaining<CreateInvoiceHandler>())
    .BuildServiceProvider();

return await new RootCommand("Invoices").AddActions(services).Parse(args).InvokeAsync();
```

```bash
mytool invoices create --customer Ana --amount 100
```

- Each dot in an action's name starts a subcommand, so `invoices.create` becomes `invoices create`.
- Each property becomes a typed option, such as `--customer`, which is required unless it's nullable or has a default value. `[Description]` on the property or record parameter becomes its help text.
- `--json` accepts all the arguments as JSON instead, for actions with nested objects or collections.
- Results are printed as JSON, so they can be piped to `jq`. Stream queries print one JSON value per line (JSON Lines) as results arrive.
- Each invocation runs in its own DI scope, and Ctrl+C cancels it.

## Exit codes

| Code | `ActionExitCodes` | Meaning |
|---|---|---|
| 0 | `Success` | The action succeeded. |
| 1 | `InvalidCommandLine` | Invalid command line, such as a missing or mistyped option. |
| 2 | `InvalidArguments` | Invalid arguments, or a message from `options.ErrorMessage`, such as validation errors. |
| 3 | `AuthenticationRequired` | No signed-in user. |
| 4 | `AccessDenied` | The user may not run the action. |
| 70 | `UnexpectedError` | Any other failure. No details are printed. |

## The current user

Register the signed-in user with `services.AddFixedPrincipalAccessor(principal)`, from Datapoint.Mediator.Authorization.DependencyInjection: for example after an OAuth device-code sign-in, or a service account. Pass `null` when no one is signed in, so actions that need a user exit with code 3.

> **Security:** if the CLI connects to the database with credentials the user can read, these rules only apply while the user goes through the CLI. For real enforcement, keep the credentials server-side, for example by having the CLI call your API.
