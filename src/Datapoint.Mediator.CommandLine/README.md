# Datapoint.Mediator.CommandLine

Adds [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/) commands, with typed options and JSON output, so a CLI offers the same commands and queries as an API or an MCP server.

```csharp
return await new RootCommand("Invoicing").AddActions(services).Parse(args).InvokeAsync();
```

```bash
invoicing invoices create --customer Ana --amount 100
```

Exit codes are 0 for success, 1 for an invalid command line, 2 for invalid arguments, 3 without a user, 4 when access is denied, and 70 for anything else, without details. They're available as `ActionExitCodes`.

**Documentation:** [Command line](https://github.com/datapoint-software/mediator/blob/main/docs/hosts/command-line.md) · [Actions](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md)
