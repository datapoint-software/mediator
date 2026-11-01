# Datapoint.Mediator.ModelContextProtocol

Offers [Datapoint.Mediator](https://www.nuget.org/packages/Datapoint.Mediator) actions as [Model Context Protocol](https://modelcontextprotocol.io) (MCP) tools, so AI agents can call your commands and queries, with the same authorization and validation as any other caller.

```csharp
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithMediatorActions();   // invoices.create becomes the tool invoices_create
```

> **Security:** an AI agent acts for a user, so it must never get more permissions than that user. Give agents a narrower principal where you can, and offer only the tools they need with `options.Filter`.

**Documentation:** [Model Context Protocol](https://github.com/datapoint-software/mediator/blob/main/docs/hosts/model-context-protocol.md) · [Actions](https://github.com/datapoint-software/mediator/blob/main/docs/actions.md)
