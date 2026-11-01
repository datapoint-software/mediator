# Command line

`Datapoint.Mediator.CommandLine` adds every [action](../actions.md) as a [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/) command, so a CLI offers the same operations as your API.

```bash
dotnet add package Datapoint.Mediator.CommandLine --prerelease
```

```csharp
await using var services = new ServiceCollection()
    .AddInvoicing()                                          // your AddMediator(...) call
    .AddFixedPrincipalAccessor(await SignInAsync())          // the signed-in user, or null
    .BuildServiceProvider();

var root = new RootCommand("Invoicing").AddActions(services);
return await root.Parse(args).InvokeAsync();
```

The full app is in [`samples/Invoicing/Invoicing.Cli`](../../samples/Invoicing/Invoicing.Cli/Program.cs).

## Commands

Each dot in an action's name starts a subcommand, and actions share their groups:

```
invoicing
└── invoices
    ├── create   --customer <customer> --amount <amount>
    ├── get      --id <id>
    ├── list     [--max <max>]
    └── cancel   --id <id> --reason <reason>
```

## Options

Each public property of the action becomes a typed option:

- **Name:** the property's name in kebab case, such as `--customer`.
- **Type:** checked by System.CommandLine, so `--amount abc` fails before the action runs.
- **Required** unless the property is nullable, or its record parameter has a default value. Required options say "(required)" in the help.
- **Help text:** `[Description]` from `System.ComponentModel`, on the property or the record parameter.

For actions with nested objects or collections, every command also accepts `--json`, with all the arguments as one JSON object. Use either `--json` or the individual options, not both:

```bash
invoicing invoices create --json '{"customer":"Ana","amount":100}'
```

> **Note:** System.CommandLine parses numbers and dates with the current culture. On a machine set to Portuguese, `--amount 42.5` is refused and `42,5` is expected. `--json` always uses invariant JSON numbers.

## Output

- Results are printed as JSON, so they can be piped to tools such as `jq`.
- A command without a result prints nothing.
- Stream queries print one JSON value per line ([JSON Lines](https://jsonlines.org/)), as results arrive, so very large streams work with pipes.
- Errors go to standard error.

```bash
invoicing invoices list | jq -r '.customer'
```

## Exit codes

| Code | `ActionExitCodes` | Meaning |
|---|---|---|
| 0 | `Success` | The action succeeded. |
| 1 | `InvalidCommandLine` | Invalid command line, such as a missing or mistyped option. The action didn't run. |
| 2 | `InvalidArguments` | Invalid arguments, or an error with a message from `ErrorMessage`, such as validation errors. |
| 3 | `AuthenticationRequired` | No signed-in user. |
| 4 | `AccessDenied` | The user may not run the action. |
| 70 | `UnexpectedError` | Any other failure. "An unexpected error occurred." is printed, with no details. |

## Errors

Map your own exceptions to a message with `ErrorMessage`. Return `null` for exceptions it doesn't handle. A mapped exception exits with code 2:

```csharp
root.AddActions(services, o => o.ErrorMessage = e => e switch
{
    ValidationException v => string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),   // never AttemptedValue
    InvoiceNotFoundException => e.Message,
    _ => null,
});
```

Unhandled exceptions are never printed, because their messages can contain connection strings or personal data. Log them yourself, for example in a behaviour, if you need the details.

## Options

```csharp
root.AddActions(services, o =>
{
    o.Filter = a => !a.Name.StartsWith("admin.");   // add only some actions
    o.SerializerOptions = mySerializerOptions;       // default: web defaults (camelCase)
    o.ErrorMessage = ...;                            // see Errors
});
```

## Scopes and cancellation

Each invocation runs in its own DI scope, created from the `IServiceProvider` you pass, and Ctrl+C cancels the running action through its `ct`.

## The current user

A CLI acts for one user for the whole run. Register them with `AddFixedPrincipalAccessor`, for example after an OAuth device-code sign-in, or from a stored token. Pass `null` when nobody is signed in: actions that need a user then exit with code 3. See [Authorization](../authorization.md#the-current-user).

> **Security:** if the CLI connects to the database with credentials the user can read, the authorization rules only apply while the user goes through the CLI: they could use the credentials directly. For real enforcement, keep credentials on a server and have the CLI call your API.
