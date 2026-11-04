using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Datapoint.Mediator.CommandLine;

// Builds the command for one action: a typed option per property, a --json fallback, and an action that dispatches it.
internal sealed class ActionCommand
{
    private static readonly MethodInfo CreateParserMethod = GetMethod(nameof(CreateParser));
    private static readonly MethodInfo CreateNumberParserMethod = GetMethod(nameof(CreateNumberParser));
    private static readonly MethodInfo UseParserMethod = GetMethod(nameof(UseParser));
    private static readonly MethodInfo UseNullableParserMethod = GetMethod(nameof(UseNullableParser));

    private readonly ActionDescriptor _action;
    private readonly IServiceProvider _services;
    private readonly MediatorCommandLineOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly List<(JsonPropertyInfo Property, Option Option, bool Required)> _properties = [];
    private readonly HashSet<string> _hostProvided;
    private readonly Option<string> _json = new("--json")
    {
        Description = "The action's arguments as a JSON object, instead of the individual options.",
    };

    private ActionCommand(ActionDescriptor action, IServiceProvider services, MediatorCommandLineOptions options)
    {
        _action = action;
        _services = services;
        _options = options;
        _serializerOptions = options.SerializerOptions ?? JsonSerializerOptions.Web;
        _hostProvided = HostProvidedNames.Of(action, _serializerOptions);
    }

    public static Command Create(string name, ActionDescriptor action, IServiceProvider services, MediatorCommandLineOptions options) =>
        new ActionCommand(action, services, options).Build(name);

    private Command Build(string name)
    {
        var command = new Command(name, _action.Description);

        // The serializer's contract, so options follow [JsonPropertyName] and [JsonIgnore] like the --json arguments do.
        foreach (var property in _serializerOptions.GetTypeInfo(_action.ActionType).Properties)
        {
            if (HasOption(property))
            {
                command.Options.Add(CreateOption(property));
            }
        }

        command.Options.Add(_json);
        command.Validators.Add(ValidateOptions);
        command.SetAction(RunAsync);
        return command;
    }

    // Only what the serializer can set gets an option. Host-provided properties get none, so the user can never believe
    // they chose the value.
    private bool HasOption(JsonPropertyInfo property)
    {
        var settable = property.Set is not null || property.AssociatedParameter is not null;
        return settable && !_hostProvided.Contains(property.Name);
    }

    // Required options are checked here rather than by System.CommandLine, because --json can replace them.
    private void ValidateOptions(CommandResult result)
    {
        var usesJson = result.GetResult(_json) is not null;
        var usesOptions = _properties.Any(p => result.GetResult(p.Option) is not null);

        if (usesJson && usesOptions)
        {
            result.AddError("Use either --json or the individual options, not both.");
            return;
        }

        if (usesJson)
        {
            return;
        }

        foreach (var (_, option, required) in _properties)
        {
            if (required && result.GetResult(option) is null)
            {
                result.AddError($"Option '{option.Name}' is required.");
            }
        }
    }

    // Named after the .NET member, so the options don't change with the app's naming policy. The value is sent under the
    // contract's name, without the DictionaryKeyPolicy, which the serializer doesn't apply to properties.
    private Option CreateOption(JsonPropertyInfo property)
    {
        var parameter = property.AssociatedParameter;
        var memberName = (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;
        var nullable = property.IsGetNullable || property.IsSetNullable || parameter?.IsNullable == true;
        var required = !nullable && parameter?.HasDefaultValue != true;
        var description = Description(property.AttributeProvider) ?? Description(parameter?.AttributeProvider);

        // Never null: Activator.CreateInstance returns null only for Nullable<T>, and Option<T> is a class.
        var option = (Option)Activator.CreateInstance(
            typeof(Option<>).MakeGenericType(property.PropertyType),
            $"--{JsonNamingPolicy.KebabCaseLower.ConvertName(memberName)}",
            Array.Empty<string>())!;
        option.Description = required ? RequiredDescription(description) : description;
        UseInvariantCulture(option, property.PropertyType);

        _properties.Add((property, option, required));
        return option;
    }

    private static string RequiredDescription(string? description) =>
        description is null ? "(required)" : $"{description} (required)";

    private static string? Description(ICustomAttributeProvider? provider) =>
        provider?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true) is [DescriptionAttribute attribute, ..]
            ? attribute.Description
            : null;

    // System.CommandLine parses with the current culture, so "42.5" would be refused, or misread, on a machine set to a
    // language that writes "42,5". Values are parsed with the invariant culture instead, like the JSON output and --json.
    // Numbers are parsed without thousands separators, because the invariant culture would otherwise read "42,5" as 425.
    // bool is left alone, because flags take no value, and so are types without IParsable, such as enums and strings,
    // whose parsing doesn't depend on culture.
    private static void UseInvariantCulture(Option option, Type propertyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        var valueType = underlyingType ?? propertyType;

        if (!valueType.IsValueType || valueType == typeof(bool) || !Implements(valueType, typeof(IParsable<>)))
        {
            return;
        }

        var isNumber = Implements(valueType, typeof(INumberBase<>));
        var createParser = isNumber ? CreateNumberParserMethod : CreateParserMethod;
        var parser = createParser.MakeGenericMethod(valueType).Invoke(null, null);
        var useParser = underlyingType is null ? UseParserMethod : UseNullableParserMethod;
        useParser.MakeGenericMethod(valueType).Invoke(null, [option, parser, InvalidValueMessage(option.Name, valueType, isNumber)]);
    }

    // Says what the option expects, never the value given or a .NET type name (ADR 0038).
    private static string InvalidValueMessage(string optionName, Type valueType, bool isNumber) =>
        !isNumber
            ? $"Option '{optionName}' has a value that isn't valid. Write dates and times in ISO 8601, such as 2026-11-01."
            : Implements(valueType, typeof(IFloatingPoint<>))
                ? $"Option '{optionName}' expects a number, with '.' as the decimal separator and no thousands separators."
                : $"Option '{optionName}' expects a whole number, with no thousands separators.";

    private delegate bool Parser<T>(string token, out T value);

    // Never null: the names come from nameof, so the methods exist.
    private static MethodInfo GetMethod(string name) =>
        typeof(ActionCommand).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool Implements(Type type, Type genericInterface) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface && i.GetGenericArguments()[0] == type);

    // TryParse can leave the value null when it fails, but the value is only read when it succeeds.
    private static Parser<T> CreateParser<T>()
        where T : IParsable<T> =>
        (string token, out T value) => T.TryParse(token, CultureInfo.InvariantCulture, out value!);

    private static Parser<T> CreateNumberParser<T>()
        where T : INumberBase<T>
    {
        var styles = Implements(typeof(T), typeof(IFloatingPoint<>)) ? NumberStyles.Float : NumberStyles.Integer;
        return (string token, out T value) => T.TryParse(token, styles, CultureInfo.InvariantCulture, out value!);
    }

    private static void UseParser<T>(Option<T> option, Parser<T> parser, string invalidMessage)
        where T : struct =>
        option.CustomParser = result => TryParse(result, parser, invalidMessage, out var value) ? value : default;

    private static void UseNullableParser<T>(Option<T?> option, Parser<T> parser, string invalidMessage)
        where T : struct =>
        option.CustomParser = result => TryParse(result, parser, invalidMessage, out var value) ? value : null;

    private static bool TryParse<T>(ArgumentResult result, Parser<T> parser, string invalidMessage, out T value)
    {
        if (parser(result.Tokens[0].Value, out value))
        {
            return true;
        }

        result.AddError(invalidMessage);
        return false;
    }

    // Everything that can fail runs inside the try, including resolving the dispatcher and disposing the scope, so no
    // exception reaches System.CommandLine's default handler, which would print its message and stack trace.
    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchAsync(parseResult, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!AdapterErrors.IsCallerCancellation(exception, cancellationToken))
        {
            var (message, code) = Describe(exception);
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(message).ConfigureAwait(false);
            return code;
        }
    }

    private async Task<int> DispatchAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        JsonElement arguments;

        if (parseResult.GetValue(_json) is { } json)
        {
            if (ReadJson(json, out arguments) is { } message)
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(message).ConfigureAwait(false);
                return ActionExitCodes.InvalidArguments;
            }
        }
        else
        {
            arguments = ReadOptions(parseResult);
        }

        // One DI scope per invocation, like a request in an API.
        var scope = _services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();
            await WriteResultsAsync(dispatcher, arguments, parseResult.InvocationConfiguration.Output, cancellationToken).ConfigureAwait(false);
        }

        return ActionExitCodes.Success;
    }

    // Returns the message to print when the --json value can't be used, or null when it can.
    private string? ReadJson(string json, out JsonElement arguments)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            arguments = default;
            return "The --json value is not valid JSON.";
        }

        // Checked before the dispatcher is resolved, so a decorator of the dispatcher can still set the value.
        if (HostProvidedNames.FindIn(_hostProvided, arguments) is { } name)
        {
            return AdapterMessages.HostProvided(name);
        }

        return null;
    }

    private JsonElement ReadOptions(ParseResult parseResult)
    {
        var values = new JsonObject();

        foreach (var (property, option, _) in _properties)
        {
            if (parseResult.GetResult(option) is { } result)
            {
                var value = result.GetValueOrDefault<object?>();
                values[property.Name] = JsonSerializer.SerializeToNode(value, property.PropertyType, _serializerOptions);
            }
        }

        return JsonSerializer.SerializeToElement(values, JsonSerializerOptions.Default);
    }

    private async Task WriteResultsAsync(
        IActionDispatcher dispatcher,
        JsonElement arguments,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (_action.Kind == ActionKind.StreamQuery)
        {
            // JSON Lines: one result per line, written as it arrives.
            var results = dispatcher.StreamAsync(_action.Name, arguments, _serializerOptions, cancellationToken);

            await foreach (var item in results.ConfigureAwait(false))
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(item, _serializerOptions)).ConfigureAwait(false);
            }

            return;
        }

        var result = await dispatcher.DispatchAsync(_action.Name, arguments, _serializerOptions, cancellationToken).ConfigureAwait(false);

        if (_action.Kind != ActionKind.Command)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(result, _serializerOptions)).ConfigureAwait(false);
        }
    }

    // Only messages known to be safe are printed. Invalid arguments and authorization failures go to the app's hook
    // first, so it can word them its own way, but the exit code stays Mediator's. Anything else is reported without
    // details.
    private (string Message, int Code) Describe(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException invalid =>
                (Word(exception) ?? AdapterMessages.InvalidArguments(_action.Name, invalid), ActionExitCodes.InvalidArguments),
            _ when AdapterErrors.IsAuthenticationRequired(exception) =>
                (Word(exception) ?? AdapterErrors.AuthenticationRequired, ActionExitCodes.AuthenticationRequired),
            _ when AdapterErrors.IsAccessDenied(exception) =>
                (Word(exception) ?? AdapterErrors.AccessDenied, ActionExitCodes.AccessDenied),
            _ when Word(exception) is { } message => (message, ActionExitCodes.InvalidArguments),
            _ => (AdapterErrors.UnexpectedError, ActionExitCodes.UnexpectedError),
        };

    // Every exception from the app's hook is caught, because nothing about it is safe to print: the default message is
    // used instead.
    private string? Word(Exception exception)
    {
        try
        {
            return _options.ErrorMessage?.Invoke(exception);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
