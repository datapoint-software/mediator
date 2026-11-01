using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
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
    private readonly List<(string JsonName, Option Option, bool Required)> _properties = [];
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
    }

    public static Command Create(string name, ActionDescriptor action, IServiceProvider services, MediatorCommandLineOptions options) =>
        new ActionCommand(action, services, options).Build(name);

    private Command Build(string name)
    {
        var command = new Command(name, _action.Description);
        var nullability = new NullabilityInfoContext();
        var parameters = _action.ActionType.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .GroupBy(parameter => parameter.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var property in _action.ActionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            parameters.TryGetValue(property.Name, out var parameter);
            var required = !IsNullable(property, nullability) && parameter?.HasDefaultValue != true;
            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description
                ?? parameter?.GetCustomAttribute<DescriptionAttribute>()?.Description;

            var option = (Option)Activator.CreateInstance(
                typeof(Option<>).MakeGenericType(property.PropertyType),
                $"--{JsonNamingPolicy.KebabCaseLower.ConvertName(property.Name)}",
                Array.Empty<string>())!;
            option.Description = required ? $"{description} (required)".TrimStart() : description;
            UseInvariantCulture(option, property.PropertyType);

            command.Options.Add(option);
            _properties.Add((_serializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name, option, required));
        }

        command.Options.Add(_json);

        // Required options are checked here rather than by System.CommandLine, because --json can replace them.
        command.Validators.Add(result =>
        {
            var usesJson = result.GetResult(_json) is not null;
            var usesOptions = _properties.Any(p => result.GetResult(p.Option) is not null);

            if (usesJson && usesOptions)
            {
                result.AddError("Use either --json or the individual options, not both.");
            }
            else if (!usesJson)
            {
                foreach (var (_, option, required) in _properties)
                {
                    if (required && result.GetResult(option) is null)
                    {
                        result.AddError($"Option '{option.Name}' is required.");
                    }
                }
            }
        });

        command.SetAction(RunAsync);
        return command;
    }

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

        var createParser = Implements(valueType, typeof(INumberBase<>)) ? CreateNumberParserMethod : CreateParserMethod;
        var parser = createParser.MakeGenericMethod(valueType).Invoke(null, null);
        var useParser = underlyingType is null ? UseParserMethod : UseNullableParserMethod;
        useParser.MakeGenericMethod(valueType).Invoke(null, [option, parser]);
    }

    private delegate bool Parser<T>(string token, out T value);

    private static MethodInfo GetMethod(string name) =>
        typeof(ActionCommand).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool Implements(Type type, Type genericInterface) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface && i.GetGenericArguments()[0] == type);

    private static Parser<T> CreateParser<T>()
        where T : IParsable<T> =>
        (string token, out T value) => T.TryParse(token, CultureInfo.InvariantCulture, out value!);

    private static Parser<T> CreateNumberParser<T>()
        where T : INumberBase<T>
    {
        var styles = Implements(typeof(T), typeof(IFloatingPoint<>)) ? NumberStyles.Float : NumberStyles.Integer;
        return (string token, out T value) => T.TryParse(token, styles, CultureInfo.InvariantCulture, out value!);
    }

    private static void UseParser<T>(Option<T> option, Parser<T> parser)
        where T : struct =>
        option.CustomParser = result => TryParse(result, option.Name, parser, out var value) ? value : default;

    private static void UseNullableParser<T>(Option<T?> option, Parser<T> parser)
        where T : struct =>
        option.CustomParser = result => TryParse(result, option.Name, parser, out var value) ? value : null;

    private static bool TryParse<T>(ArgumentResult result, string optionName, Parser<T> parser, out T value)
    {
        var token = result.Tokens[0].Value;

        if (parser(token, out value))
        {
            return true;
        }

        result.AddError($"Cannot parse argument '{token}' for option '{optionName}' as expected type '{typeof(T)}'.");
        return false;
    }

    private static bool IsNullable(PropertyInfo property, NullabilityInfoContext nullability) =>
        property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : nullability.Create(property).WriteState is NullabilityState.Nullable
              || nullability.Create(property).ReadState is NullabilityState.Nullable;

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken ct)
    {
        var output = parseResult.InvocationConfiguration.Output;
        var error = parseResult.InvocationConfiguration.Error;
        JsonElement arguments;

        if (parseResult.GetValue(_json) is { } json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                arguments = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                await error.WriteLineAsync("The --json value is not valid JSON.").ConfigureAwait(false);
                return ActionExitCodes.InvalidArguments;
            }
        }
        else
        {
            var values = new Dictionary<string, object?>();

            foreach (var (jsonName, option, _) in _properties)
            {
                if (parseResult.GetResult(option) is { } result)
                {
                    values[jsonName] = result.GetValueOrDefault<object?>();
                }
            }

            arguments = JsonSerializer.SerializeToElement(values, _serializerOptions);
        }

        // One DI scope per invocation, like a request in an API.
        await using var scope = _services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();

        try
        {
            if (_action.Kind == ActionKind.StreamQuery)
            {
                // JSON Lines: one result per line, written as it arrives.
                await foreach (var item in dispatcher.StreamAsync(_action.Name, arguments, _serializerOptions, ct).ConfigureAwait(false))
                {
                    await output.WriteLineAsync(JsonSerializer.Serialize(item, _serializerOptions)).ConfigureAwait(false);
                }
            }
            else
            {
                var result = await dispatcher.DispatchAsync(_action.Name, arguments, _serializerOptions, ct).ConfigureAwait(false);

                if (_action.Kind != ActionKind.Command)
                {
                    await output.WriteLineAsync(JsonSerializer.Serialize(result, _serializerOptions)).ConfigureAwait(false);
                }
            }

            return ActionExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var (message, code) = Describe(exception);
            await error.WriteLineAsync(message).ConfigureAwait(false);
            return code;
        }
    }

    // Only messages known to be safe are printed. Anything else is reported without details.
    private (string Message, int Code) Describe(Exception exception) =>
        exception switch
        {
            InvalidActionArgumentsException => (exception.Message, ActionExitCodes.InvalidArguments),
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AuthenticationRequiredException" =>
                ("Authentication is required.", ActionExitCodes.AuthenticationRequired),
            _ when exception.GetType().FullName == "Datapoint.Mediator.Authorization.AccessDeniedException" =>
                ("Access is denied.", ActionExitCodes.AccessDenied),
            _ when _options.ErrorMessage?.Invoke(exception) is { } message => (message, ActionExitCodes.InvalidArguments),
            _ => ("An unexpected error occurred.", ActionExitCodes.UnexpectedError),
        };
}
