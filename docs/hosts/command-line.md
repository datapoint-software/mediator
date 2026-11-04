# Command line

`Datapoint.Mediator.CommandLine` adds every [action](../actions.md) marked `[CommandLine]` as a [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/) command, so a CLI offers the same operations as your API.

```bash
dotnet add package Datapoint.Mediator.CommandLine --prerelease
```

```csharp
await using var services = new ServiceCollection()
    .AddInvoicing()                                          // your AddMediator(...) call
    .AddFixedPrincipalAccessor(await SignInAsync())          // the signed-in user, or null
    .BuildServiceProvider();

var root = new RootCommand("Invoicing").AddMediatorActions(services);
return await root.Parse(args).InvokeAsync();
```

The full app is in [`samples/Invoicing/Invoicing.Cli`](../../samples/Invoicing/Invoicing.Cli/Program.cs).

## Commands

An action without `[CommandLine]` isn't added, and running it fails like running an unknown command. A CLI that sends its actions to your API, instead of running them in-process, needs them marked `[Api]` too, for the server. See [Where an action is offered](../actions.md#where-an-action-is-offered).

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

Each property the JSON serializer can set becomes a typed option. The options follow the action's JSON contract, like `--json` does: a property marked `[JsonIgnore]`, or a read-only property, gets no option, and a property marked `[JsonPropertyName]` is sent under that name.

- **Name:** the property's .NET name in kebab case, such as `--customer`, whatever the JSON name or naming policy.
- **Type:** checked by System.CommandLine, so `--amount abc` fails before the action runs.
- **Required** unless the property is nullable, or its record parameter has a default value. Required options say "(required)" in the help.
- **Help text:** `[Description]` from `System.ComponentModel`, on the property or the record parameter.

For actions with nested objects or collections, every command also accepts `--json`, with all the arguments as one JSON object. Use either `--json` or the individual options, not both:

```bash
invoicing invoices create --json '{"customer":"Ana","amount":100}'
```

### Host-provided properties

A property marked [`[HostProvided]`](../actions.md#host-provided-properties), such as the tenant of the user's session, gets no option and is never required, so `--help` doesn't mention it. A `--json` value for it, in any letter case, is refused before the action runs, with exit code 2:

```text
$ invoicing invoices list --json '{"tenantId":"6f1c..."}'
'tenantId' is set by the server and cannot be given.
```

The CLI never sends the property. The host must set it: an API the CLI calls overwrites it, and a CLI that runs the actions itself sets it in a [decorator of `IActionDispatcher`](../actions.md#filling-the-value). Hiding it is presentation only.

Numbers, dates and times are parsed the same way on every machine, whatever its language, so scripts behave the same everywhere ([ADR 0038](../adr/0038-cli-parse-errors-name-the-expected-format.md)):
- numbers use a dot as the decimal separator, with no thousands separators: `--amount 42.5`, never `42,5` or `1,000`;
- dates and times use ISO 8601: `--day 2026-11-01`, `--notify-at 2026-11-01T09:30:00+00:00`.

A value that can't be parsed exits with code 1, and the message says what the option expects, never the value given: "Option '--amount' expects a number, with '.' as the decimal separator and no thousands separators." System.CommandLine's own syntax errors, such as an unrecognised argument, still repeat what was typed.

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
| 2 | `InvalidArguments` | Invalid arguments, or another exception with a message from `ErrorMessage`, such as validation errors. |
| 3 | `AuthenticationRequired` | No signed-in user. |
| 4 | `AccessDenied` | The user may not run the action. |
| 70 | `UnexpectedError` | Any other failure, including one while resolving services or disposing the invocation's scope. "An unexpected error occurred." is printed, with no details. |

## Errors

Invalid `--json` arguments exit with code 2, naming the argument that failed when it's known:

```text
The argument 'amount' of action 'invoices.create' is not valid.
```

A missing argument, or `null` where it isn't accepted, reads "... is required." instead.

See [Invalid arguments](../actions.md#invalid-arguments) for how the argument is spelled. The messages Mediator writes never contain argument values, .NET type names, stack traces or paths.

Map your own exceptions to a message with `ErrorMessage`. Return `null` for exceptions it doesn't handle. A mapped exception exits with code 2. `ErrorMessage` also receives `InvalidActionArgumentsException`, `AuthenticationRequiredException` and `AccessDeniedException` first, so you can word them your own way, and `null` keeps Mediator's text. If `ErrorMessage` throws, Mediator's text is used too. Their exit codes stay 2, 3 and 4 whatever it returns:

```csharp
root.AddMediatorActions(services, o => o.ErrorMessage = e => e switch
{
    InvalidActionArgumentsException { Field: { } field } => $"invalid: {field}",
    AuthenticationRequiredException => "authentication_required: sign in with 'invoices login'.",
    AccessDeniedException => "access_denied",
    ValidationException v => string.Join("; ", v.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),   // never AttemptedValue
    InvoiceNotFoundException => "The invoice doesn't exist.",
    _ => null,
});
```

Don't say why access was denied, such as a missing role or who owns a resource: that tells a caller how to get in.

Unhandled exceptions are never printed, because their messages can contain connection strings or personal data. Log them yourself, for example in a behaviour, if you need the details.

> **Security:** return only text you wrote. An exception's `Message` is for logs, including your own exceptions': a message that later gains an id or a value would be printed without anyone changing the hook. For invalid arguments, never return `InnerException.Message`, which can contain .NET type names and property names the user chose.

## Options

```csharp
root.AddMediatorActions(services, o =>
{
    o.Filter = a => !a.Name.StartsWith("admin.");   // add only some of the [CommandLine] actions
    o.SerializerOptions = mySerializerOptions;       // default: web defaults (camelCase)
    o.ErrorMessage = ...;                            // see Errors
});
```

## Scopes and cancellation

Each invocation runs in its own DI scope, created from the `IServiceProvider` you pass, and Ctrl+C cancels the running action through its `cancellationToken`. An `OperationCanceledException` counts as Ctrl+C only when that token is cancelled: a handler's own timeout is reported as an unexpected error, with exit code 70.

## The current user

A CLI acts for one user for the whole run. Register them with `AddFixedPrincipalAccessor`, for example after an OAuth device-code sign-in, or from a stored token. Pass `null` when nobody is signed in: actions that need a user then exit with code 3. See [Authorization](../authorization.md#the-current-user).

> **Security:** if the CLI connects to the database with credentials the user can read, the authorization rules only apply while the user goes through the CLI: they could use the credentials directly. For real enforcement, keep credentials on a server and have the CLI call your API.
