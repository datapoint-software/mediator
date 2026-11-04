namespace Datapoint.Mediator;

// The messages the adapters that write plain text, the CLI and the MCP tools, share. Linked as source into both.
internal static class AdapterMessages
{
    // Built from the field and the reason only, never from the exception's message.
    public static string InvalidArguments(string actionName, InvalidActionArgumentsException exception) =>
        AdapterErrors.SafeField(exception) is { } field
            ? $"The argument '{field}' of action '{actionName}' is {(AdapterErrors.IsRequired(exception) ? "required" : "not valid")}."
            : $"The arguments for action '{actionName}' are not valid.";

    // Names the contract's spelling of the property, never the caller's.
    public static string HostProvided(string name) => $"'{name}' is set by the server and cannot be given.";
}
