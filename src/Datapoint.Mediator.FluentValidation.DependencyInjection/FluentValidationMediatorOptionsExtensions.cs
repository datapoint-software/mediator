using System;
using Datapoint.Mediator;
using Datapoint.Mediator.FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FluentValidation support in <see cref="MediatorOptions"/>.
/// </summary>
public static class FluentValidationMediatorOptionsExtensions
{
    /// <summary>
    /// Registers <see cref="ValidationBehavior"/> as a scoped behaviour, so that commands and queries are validated
    /// before their handler runs.
    /// </summary>
    /// <param name="options">The mediator options.</param>
    /// <returns>The mediator options, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The behaviour runs at the position it is registered in, relative to other behaviours. Register it after any
    /// behaviour that should handle validation failures, such as an exception-handling behaviour.
    /// </para>
    /// <para>
    /// Validators are not registered by this method. Register them with FluentValidation, for example with
    /// <c>services.AddValidatorsFromAssemblyContaining&lt;T&gt;()</c> from FluentValidation.DependencyInjectionExtensions.
    /// </para>
    /// <para>
    /// The behaviour is always scoped, whatever <see cref="MediatorOptions.DefaultLifetime"/> is, so that it resolves
    /// validators from the current scope. Calling this method more than once has no effect.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static MediatorOptions AddValidationBehavior(this MediatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior<ValidationBehavior>(ServiceLifetime.Scoped);
    }
}
