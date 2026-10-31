using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;

namespace Datapoint.Mediator.FluentValidation;

/// <summary>
/// Validates commands, queries and stream queries with their FluentValidation validators before their handler runs.
/// </summary>
/// <remarks>
/// <para>
/// For each command, query or stream query, the behaviour resolves every <see cref="IValidator{T}"/> registered for its exact type and
/// runs them one after another. If any validator reports a failure, it throws a <see cref="ValidationException"/>
/// containing the failures from all validators, and the handler is not called. Messages are not validated.
/// </para>
/// <para>
/// A stream query is validated when its results are first enumerated, before the handler produces any result.
/// Validators must be registered for the exact command or query type; validators for a base class or interface are not
/// used. Create the behaviour from the current scope, so that scoped validators are resolved from it.
/// </para>
/// <para>
/// <b>Security:</b> each <see cref="ValidationFailure"/> in <see cref="ValidationException.Errors"/> carries the
/// submitted value in <see cref="ValidationFailure.AttemptedValue"/>, which can contain passwords or personal data. Do not
/// log the exception or its failures as they are, and do not return them to clients unchanged. When building an error
/// response, such as ASP.NET Core <c>ProblemDetails</c>, include only <see cref="ValidationFailure.PropertyName"/> and
/// <see cref="ValidationFailure.ErrorMessage"/>.
/// </para>
/// </remarks>
public sealed class ValidationBehavior : IBehavior
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorsTypes = new();

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehavior"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve validators from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public ValidationBehavior(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="ValidationException">The command is not valid.</exception>
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default)
    {
        var validators = GetValidators(command);
        return validators.Length == 0 ? next(ct) : ValidateThenHandleAsync(command, validators, next, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ValidationException">The command is not valid.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default)
    {
        var validators = GetValidators(command);
        return validators.Length == 0 ? next(ct) : ValidateThenHandleAsync(command, validators, next, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ValidationException">The query is not valid.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default)
    {
        var validators = GetValidators(query);
        return validators.Length == 0 ? next(ct) : ValidateThenHandleAsync(query, validators, next, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ValidationException">The stream query is not valid. Thrown when the results are first enumerated.</exception>
    public IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        CancellationToken ct = default)
    {
        var validators = GetValidators(query);
        return validators.Length == 0 ? next(ct) : ValidateThenStreamAsync(query, validators, next, ct);
    }

    private IValidator[] GetValidators(object action)
    {
        var validatorsType = ValidatorsTypes.GetOrAdd(
            action.GetType(),
            static type => typeof(IEnumerable<>).MakeGenericType(typeof(IValidator<>).MakeGenericType(type)));

        // Containers other than Microsoft's may return null or a non-array enumerable.
        return _serviceProvider.GetService(validatorsType) switch
        {
            IValidator[] array => array,
            IEnumerable<IValidator> enumerable => enumerable.ToArray(),
            _ => [],
        };
    }

    private static async ValueTask ValidateThenHandleAsync(
        object action,
        IValidator[] validators,
        Func<CancellationToken, ValueTask> next,
        CancellationToken ct)
    {
        await ValidateAsync(action, validators, ct).ConfigureAwait(false);
        await next(ct).ConfigureAwait(false);
    }

    private static async ValueTask<TResult> ValidateThenHandleAsync<TResult>(
        object action,
        IValidator[] validators,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct)
    {
        await ValidateAsync(action, validators, ct).ConfigureAwait(false);
        return await next(ct).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<TResult> ValidateThenStreamAsync<TResult>(
        object action,
        IValidator[] validators,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await ValidateAsync(action, validators, ct).ConfigureAwait(false);

        await foreach (var result in next(ct).ConfigureAwait(false))
        {
            yield return result;
        }
    }

    // Runs every validator, one after another, and throws once with all of their failures.
    private static async ValueTask ValidateAsync(object action, IValidator[] validators, CancellationToken ct)
    {
        List<ValidationFailure>? failures = null;

        foreach (var validator in validators)
        {
            // A new context per validator, because a context accumulates the failures of every validator it is given.
            var result = await validator.ValidateAsync(new ValidationContext<object>(action), ct).ConfigureAwait(false);

            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is not null)
        {
            throw new ValidationException(failures);
        }
    }
}
