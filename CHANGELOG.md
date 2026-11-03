# Changelog

All notable changes to the Datapoint.Mediator packages are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the packages follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

## [4.0.0-beta.6](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.6) - 2026-11-03

### Added

- **Breaking:** Say why an argument couldn't be read ([f79ddb5](https://github.com/datapoint-software/mediator/commit/f79ddb53bf3cde4ef5822b6e0d617f794d221d43))
- **Breaking:** Let error hooks word authentication and access-denied failures ([599db88](https://github.com/datapoint-software/mediator/commit/599db8849cae0406f8958a7a6917165ad5234123))
- **Breaking:** **model-context-protocol:** Return errors as tool results ([75b8075](https://github.com/datapoint-software/mediator/commit/75b80756a1771df1cf5bec00f09db9f320b5dcaf))

## [4.0.0-beta.5](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.5) - 2026-11-03

### Added

- **Breaking:** **abstractions:** Require a description for every action ([4468664](https://github.com/datapoint-software/mediator/commit/446866452d3719350e7f37944d0a3d2e4edf937e))
- **Breaking:** **abstractions:** Declare the adapters that offer an action ([f6bc4a5](https://github.com/datapoint-software/mediator/commit/f6bc4a536997ef789dff2284ff90dbef0efdad69))
- **Breaking:** **aspnetcore:** Offer only actions marked [Api] ([b026b68](https://github.com/datapoint-software/mediator/commit/b026b6895a4696f121a80e0c06bd0a056f1ed335))
- **Breaking:** **model-context-protocol:** Offer only actions marked [ModelContextProtocol] ([5c3ac54](https://github.com/datapoint-software/mediator/commit/5c3ac546e539f789c7be990794a6d9d21b0f8d4d))
- **Breaking:** **command-line:** Offer only actions marked [CommandLine] ([26cc402](https://github.com/datapoint-software/mediator/commit/26cc402fbfa6c22185b99862d7e9d82c49e91f07))

### Documentation

- **command-line:** Remove an outdated copy of the guide's sections ([bcf180d](https://github.com/datapoint-software/mediator/commit/bcf180ded3b2c4e35e61e9635d7a2ac0bb5b5c6a))

### Migrating from 4.0.0-beta.4

These changes are breaking: review each one before adopting `4.0.0-beta.5`. An application that hasn't migrated fails at startup, in `AddMediator`, so nothing is exposed by mistake.

- **Every action needs a description,** as the second argument of `[Action]`. Startup refuses an empty or whitespace one.

  | Before | After |
  |---|---|
  | `[Action("invoices.create", Description = "Creates an invoice.")]` | `[Action("invoices.create", "Creates an invoice.")]` |
  | `[Action("invoices.create")]` | `[Action("invoices.create", "Creates an invoice.")]` |

  `ActionDescriptor.Description` is no longer nullable, and its constructor requires a description.
- **Mark each action with the hosts that offer it:** `[Api]`, `[CommandLine]` or `[ModelContextProtocol]`. A host now offers only the actions marked for it, and an action without any of them fails at startup, with every such action listed. To keep offering an action everywhere, as before, mark it with all three:

  ```csharp
  [Api]
  [CommandLine]
  [ModelContextProtocol]
  [Action("invoices.create", "Creates an invoice.")]
  public sealed record CreateInvoice(string Customer, decimal Amount) : ICommand<Guid>;
  ```

- **`Hidden` is removed.** Remove `[Action]` from actions that had `Hidden = true`, and keep sending them through `IMediator`.
- **`Filter` only narrows.** It now sees only the actions marked for its host. A filter that removed actions from one host can become a missing attribute instead.
- **A CLI that dispatches to your API** needs its actions marked `[CommandLine]` and `[Api]`.
- **Custom hosts** check their own attribute, derived from `ActionChannelAttribute`, with `action.IsOfferedBy<TChannel>()` before offering or dispatching an action. `IActionDispatcher` doesn't check channels.

See [ADR 0031](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0031-every-action-has-a-description.md) and [ADR 0032](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0032-actions-declare-the-adapters-that-offer-them.md) for the decisions, and [Where an action is offered](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md#where-an-action-is-offered) for the guide.

## [4.0.0-beta.4](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.4) - 2026-11-03

### Added

- **abstractions:** Add ActionName and Field to InvalidActionArgumentsException ([c76684c](https://github.com/datapoint-software/mediator/commit/c76684c7f9fd9fd4f72387f153259eedbaf7e6c7))
- **mediator:** Name the failing argument and add ActionArguments ([8fc008c](https://github.com/datapoint-software/mediator/commit/8fc008c5fd3d1a91197f2997dc370f6f517adc26))
- **Breaking:** **aspnetcore:** Report argument errors as validation problems and pass them to MapException ([a9b7fbb](https://github.com/datapoint-software/mediator/commit/a9b7fbbf90ea5836dabb29ec1f0b8e5bf9c69670))
- **Breaking:** **model-context-protocol:** Name the failing argument and pass argument errors to ErrorMessage ([8e1deea](https://github.com/datapoint-software/mediator/commit/8e1deea278a88eabce9c9e98a2fc7b9fe15c15f5))
- **Breaking:** **command-line:** Name the failing argument and pass argument errors to ErrorMessage ([2a61d5e](https://github.com/datapoint-software/mediator/commit/2a61d5e87a0b5dca1da3fedfd8f2efc43c6ee489))

### Documentation

- Record that argument errors name the field, and that hooks write their own text ([a7b1d29](https://github.com/datapoint-software/mediator/commit/a7b1d2980cb585f531801944e2989eb1584279e1))

### Refactor

- **Breaking:** Prefix host registration methods with Mediator ([cb865ff](https://github.com/datapoint-software/mediator/commit/cb865ffa1a0caec25ed71bd8e5ce062a5c8e56c0))

### Migrating from 4.0.0-beta.3

These changes are breaking: review each one before adopting `4.0.0-beta.4`.

- **Renamed registration methods:** `app.MapActions(...)` is now `app.MapMediatorActions(...)`, and `root.AddActions(...)` is now `root.AddMediatorActions(...)`. Their extension classes are now `MediatorEndpointRouteBuilderExtensions` and `MediatorCommandExtensions`, in the same namespaces. `WithMediatorActions` is unchanged. See [ADR 0030](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0030-registration-methods-carry-the-mediator-prefix.md).
- **Invalid arguments name the field.** `InvalidActionArgumentsException` has `ActionName` and `Field`, such as `count` or `lines[1].quantity`, built only from the action's contract. The default texts change:
  - HTTP: a `400` `ValidationProblemDetails` with `"errors": { "count": ["The argument is not valid."] }` when the field is known; otherwise the `ProblemDetails` "The arguments for action '<name>' are not valid.".
  - MCP and CLI: "The argument 'count' of action '<name>' is not valid.", or the generic text.
  - Update tests or clients that match the old texts.
- **Hooks see invalid arguments first.** `MapException` and `ErrorMessage` now receive `InvalidActionArgumentsException` before Mediator writes its own response; returning `null` keeps it. A hook that returns a value for every exception now also replaces that response: make it return `null` for exceptions it doesn't recognise.
- **Hosts no longer print the exception's message.** If your code throws `InvalidActionArgumentsException` itself and wants its own text shown, return that text from `ErrorMessage` or `MapException`. Use the new constructor to pass the field.
- **Hooks should write their own text.** An exception's `Message` is for logs, including your own exceptions'. Never return `InnerException.Message` for invalid arguments.
- **Optional:** use options based on `JsonSerializerOptions.Strict`, with camelCase names, so missing, `null` and unknown arguments are refused, and check arguments locally with `ActionArguments.Deserialize`. See [Invalid arguments](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md#invalid-arguments).
- `CustomizeProblemDetails` keeps running on Mediator's own `400`, `401` and `403` responses, and is now covered by tests.

See [ADR 0029](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0029-argument-errors-name-the-field.md) for the decision.

## [4.0.0-beta.3](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.3) - 2026-11-02

### Added

- **abstractions:** Add HostProvidedAttribute ([b490ebe](https://github.com/datapoint-software/mediator/commit/b490ebe1a66cad30624f61d36db53df7298c19db))
- **command-line:** Hide and refuse host-provided properties ([00149ca](https://github.com/datapoint-software/mediator/commit/00149ca90c317f5cb52381cd40a2d722f532abed))
- **model-context-protocol:** Hide and refuse host-provided properties ([d2f94d4](https://github.com/datapoint-software/mediator/commit/d2f94d47a2ab2128482964432f9bc679e356ccee))

### Documentation

- Use a neutral DbContext name in examples ([f86460b](https://github.com/datapoint-software/mediator/commit/f86460ba6de81ccf391e38cce88f96b4bb68799b))
- **aspnetcore:** Mark host-provided properties read-only in OpenAPI ([be16a72](https://github.com/datapoint-software/mediator/commit/be16a722b33767491c4759ed63d41c9e0abb321c))

### Migrating from 4.0.0-beta.2

No breaking changes: adopt `4.0.0-beta.3` as is. To stop asking callers for values the host supplies, such as the tenant of the caller's session:

- Mark those properties, or record parameters, `[HostProvided]`. The CLI then adds no option for them, MCP tools leave them out of their input schema, and both refuse a value a caller gives for them.
- The host must still set or overwrite the value: hiding it is presentation only. An API overwrites it in the request body; a CLI or MCP server that dispatches in-process sets it in a decorator of `IActionDispatcher`.
- A host that writes those values into the caller's CLI or MCP arguments, for example in MCP middleware that edits `tools/call` arguments, must move that into the decorator, or every call is refused.
- For OpenAPI, add the schema transformer from the [ASP.NET Core guide](https://github.com/datapoint-software/mediator/blob/main/docs/hosts/aspnetcore.md#host-provided-properties).

See [ADR 0028](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0028-host-provided-properties.md) for the decision.

## [4.0.0-beta.2](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.2) - 2026-11-01

### Added

- **Breaking:** **authorization:** Return decisions with details from authorizers ([723713c](https://github.com/datapoint-software/mediator/commit/723713c3ef4333a5faad4feb8faa608e0b62c66f))
- **authorization:** Expose decisions to the dispatch and observe them ([063bccb](https://github.com/datapoint-software/mediator/commit/063bccbd5a64bf2adb76cfea91355db7d2597829))
- **dependency-injection:** Register behaviours created by a factory ([288f0ba](https://github.com/datapoint-software/mediator/commit/288f0bafb39444beb7e63fbc12cd987bd4cbc16c))
- **Breaking:** **authorization:** Bind decisions to their request and hide them from messages ([d0d4ec4](https://github.com/datapoint-software/mediator/commit/d0d4ec44db0f8cb80eff1f1bcd30a6474a924a4f))
- **Breaking:** **dependency-injection:** Refuse conflicting behaviour registrations and add startup checks ([8a918b0](https://github.com/datapoint-software/mediator/commit/8a918b0aaab40735713159040651cf76a0687875))
- **abstractions:** Add ITransactionContext ([a23946d](https://github.com/datapoint-software/mediator/commit/a23946d4732801497f7bdfb257eceab373b0e4a0))
- **Breaking:** **entity-framework-core:** Add transaction options, initializers and read-only query transactions ([269042d](https://github.com/datapoint-software/mediator/commit/269042d29861260e7a421664d379960d2495292b))
- **authorization:** Add AuthenticationBehavior and RequireTransaction ([d154ff2](https://github.com/datapoint-software/mediator/commit/d154ff23a88d825727846b44ddd140c9bcaa1983))

### Migrating from 4.0.0-beta.1

- **Authorizers:** `IAuthorizer<T>.IsAuthorizedAsync` (returning `bool`) is now `AuthorizeAsync`, returning an `AuthorizationDecision`. Replace `new(condition)` with `new(AuthorizationDecision.AllowIf(condition))`, or return `AuthorizationDecision.Allow(details)` / `Deny(details)`. Details never reach callers: adapters still only report 401 or 403.
- **`Datapoint.Mediator.EntityFrameworkCore`** now depends on `Microsoft.EntityFrameworkCore.Relational`.

Everything else is new and opt-in: decision details, `IAuthorizationContext`, observers, `AuthenticationBehavior`, transaction options, initializers, read-only query transactions and `ITransactionContext`. See [ADR 0020](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0020-commands-authorized-inside-their-transaction.md) to [ADR 0027](https://github.com/datapoint-software/mediator/blob/main/docs/adr/0027-startup-checks.md) for the decisions.

## [4.0.0-beta.1](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-beta.1) - 2026-11-01

### Added

- **Breaking:** Add stream queries ([7d25acb](https://github.com/datapoint-software/mediator/commit/7d25acb90e8faacaf6742d2dfe2d9fa508433a4d))
- **dependency-injection:** Register stream query handlers ([439c7f7](https://github.com/datapoint-software/mediator/commit/439c7f7225f148b7ffb25409e0029a71b72313ef))
- **fluent-validation:** Validate stream queries ([765aec1](https://github.com/datapoint-software/mediator/commit/765aec18302adf1c387041671c281b36b70bdaa8))
- **mediator:** Add tracing and metrics ([91f8e79](https://github.com/datapoint-software/mediator/commit/91f8e7902d8dbf578d0bc5bba0a20d7fad2d68e5))
- **authorization:** Add AuthorizationBehavior ([4ffeb5d](https://github.com/datapoint-software/mediator/commit/4ffeb5d6e0dfc3e850ac9dd239fcec5ad8b6aa78))
- **authorization:** Add AddAuthorizationBehavior and authorizer scanning ([abad703](https://github.com/datapoint-software/mediator/commit/abad7037e76eebc6ab9247aecb09a95eb096f193))
- **entity-framework-core:** Add TransactionBehavior ([479d094](https://github.com/datapoint-software/mediator/commit/479d0945a442aacd192e54d45875f3b80839d093))
- **entity-framework-core:** Add AddTransactionBehavior ([9047def](https://github.com/datapoint-software/mediator/commit/9047deffc002ee6b7cb0b2ddefe5fe5c1f157cf4))
- **abstractions:** Add action naming and catalog contracts ([1c6abe4](https://github.com/datapoint-software/mediator/commit/1c6abe4d6a9e99e84178ebffa4bd04842f6e8628))
- **mediator:** Add action catalog and dispatcher ([c1bbc6b](https://github.com/datapoint-software/mediator/commit/c1bbc6bcfb3052dfd0f9c9045295bbf24ae9f0b8))
- **dependency-injection:** Build the action catalog from registered handlers ([fa24aba](https://github.com/datapoint-software/mediator/commit/fa24aba4a70ac0fef522a39db9d18cf5ecda8342))
- **mediator:** Restrict action names to dots and hyphens ([ed64d2d](https://github.com/datapoint-software/mediator/commit/ed64d2d7ffee0517d453e708209e469010c00049))
- **model-context-protocol:** Offer actions as MCP tools ([e2669ee](https://github.com/datapoint-software/mediator/commit/e2669ee0dbb492af5fd0fb244f79b8324d754060))
- **aspnetcore:** Map actions as HTTP endpoints ([515472f](https://github.com/datapoint-software/mediator/commit/515472f1428f724c03405e36fdc5e5659709335c))
- **command-line:** Add actions as CLI commands ([5e33020](https://github.com/datapoint-software/mediator/commit/5e33020d8fb6cd9ea94b901017a8fd6c3993f413))
- **authorization:** Add FixedPrincipalAccessor ([2f3f186](https://github.com/datapoint-software/mediator/commit/2f3f186e3d73a1b004d8bf75f64047b75ead4f98))
- **authorization:** Add AddFixedPrincipalAccessor ([7760535](https://github.com/datapoint-software/mediator/commit/7760535ac5d84528bbd3693f62c607985970c00f))
- **authorization-aspnetcore:** Add HttpPrincipalAccessor ([4e2336e](https://github.com/datapoint-software/mediator/commit/4e2336e8aad70202463d7fe85916d7eda229a112))
- **command-line:** Add ActionExitCodes.InvalidCommandLine ([e83c47e](https://github.com/datapoint-software/mediator/commit/e83c47ebb40a611a022a8331a491667974398809))

### Fixed

- **command-line:** Parse numbers and dates with the invariant culture ([c406a99](https://github.com/datapoint-software/mediator/commit/c406a998481a9713a73420044775e3768d9f0ac2))

### Documentation

- Document stream queries and telemetry ([229c074](https://github.com/datapoint-software/mediator/commit/229c0741ae22e16687f3c12c8c0c75a7c92ec147))
- Document new packages and behaviour order ([5a7264b](https://github.com/datapoint-software/mediator/commit/5a7264bd4348482065e9088a0f6e2efae5a3ac76))
- Add README badges ([9c45401](https://github.com/datapoint-software/mediator/commit/9c45401616cbbf3cca33a271955a7d97183c01b7))
- Add coverage badge and policy ([08b4a5e](https://github.com/datapoint-software/mediator/commit/08b4a5e23e2603a10f8cf5823f4e5ba99b20265d))
- Add security policy ([aeac45f](https://github.com/datapoint-software/mediator/commit/aeac45f3684efc02f468cbf161edcfb11bbb0af2))
- **authorization:** Warn against publishing messages from user input ([5ec7fbd](https://github.com/datapoint-software/mediator/commit/5ec7fbd1896ac6d6ce995b64b5f1b37f1f8dba7b))
- Document actions ([4f3c183](https://github.com/datapoint-software/mediator/commit/4f3c1831a723277d2556c8bc9f614b0c8925bc59))
- Document the MCP adapter ([b3f8381](https://github.com/datapoint-software/mediator/commit/b3f83815c5a0cb6e5cf1efd3111d58e7713357aa))
- Document the ASP.NET Core and CLI adapters ([52b8a2a](https://github.com/datapoint-software/mediator/commit/52b8a2a2dfb140cfc0797d75becedbd2a4d84aa6))
- **entity-framework-core:** Document retry and nesting caveats ([99ab3b9](https://github.com/datapoint-software/mediator/commit/99ab3b957324271623a49ea8c7d3bf37d00ef2bc))
- Add a changelog generated with git-cliff ([cf6c74c](https://github.com/datapoint-software/mediator/commit/cf6c74cc9d4cc88f23f3f3f4220f6d9349cf2ead))
- Document the ready-made principal accessors ([3cd42bb](https://github.com/datapoint-software/mediator/commit/3cd42bb0c87196b44982db9037c03f754e5821eb))
- Add architecture decision records ([ecf0500](https://github.com/datapoint-software/mediator/commit/ecf0500d7d65b98a4af33d69dbe9ba7e97169b55))
- Add the architecture overview ([ecadbc1](https://github.com/datapoint-software/mediator/commit/ecadbc1eb5688f4d38f90eac29df74e5b7cf598c))
- Add the contributing guide ([26f48e9](https://github.com/datapoint-software/mediator/commit/26f48e98bec376976e7443dc0b96e27982d3f830))
- **samples:** Add invoicing samples for API, CLI and MCP ([8ff3ca6](https://github.com/datapoint-software/mediator/commit/8ff3ca6f2310100f8059d154490dc25ee1fb97f2))
- Add the getting started, concepts and pipeline guides ([9cd7560](https://github.com/datapoint-software/mediator/commit/9cd756066c717c4a08f0c380e299cb3b7bf3c9de))
- Add the feature guides ([1584379](https://github.com/datapoint-software/mediator/commit/158437955dad97888dd9db84233c3c1ea172b2fd))
- Add the host guides ([7d91822](https://github.com/datapoint-software/mediator/commit/7d918224c62ff10226e32360ecc50faae03d0102))
- Add the telemetry, testing, security and use case guides ([98d70b0](https://github.com/datapoint-software/mediator/commit/98d70b06e5c0a069d183447621347d9826bc6294))
- Shorten package READMEs and link to the manual ([824b2a2](https://github.com/datapoint-software/mediator/commit/824b2a23bb18d3964913d19c3e8d3c7709fe1b34))
- Add maintenance rules to CLAUDE.md ([1a981c6](https://github.com/datapoint-software/mediator/commit/1a981c6a09bfe8553078cbbac63f474eeb54a06f))
- Record and document invariant culture parsing in the CLI ([9d566a1](https://github.com/datapoint-software/mediator/commit/9d566a19d7c5c7955108929fa169d5ee8fa64ac5))

## [4.0.0-alpha.1](https://github.com/datapoint-software/mediator/releases/tag/4.0.0-alpha.1) - 2026-10-31

Initial public release.

### Added

- `Datapoint.Mediator.Abstractions`: commands, queries, messages, their handlers, behaviours and `IMediator`.
- `Datapoint.Mediator`: the mediator implementation.
- `Datapoint.Mediator.DependencyInjection`: `AddMediator`, with handler and behaviour registration and assembly scanning.
- `Datapoint.Mediator.FluentValidation`: a behaviour that validates commands and queries with FluentValidation.
- `Datapoint.Mediator.FluentValidation.DependencyInjection`: `AddValidationBehavior`.
