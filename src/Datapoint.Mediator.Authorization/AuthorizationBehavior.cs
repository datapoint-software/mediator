using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Authorizes commands, queries and stream queries for the current user before their handler runs, denying access by
/// default.
/// </summary>
/// <remarks>
/// <para>
/// For each command, query or stream query:
/// </para>
/// <list type="number">
/// <item><description>An action marked with <see cref="AllowAnonymousAccessAttribute"/> is allowed.</description></item>
/// <item><description>
/// Otherwise, if <see cref="IPrincipalAccessor"/> returns no user, or one that is not authenticated, an
/// <see cref="AuthenticationRequiredException"/> is thrown.
/// </description></item>
/// <item><description>
/// If no <see cref="IAuthorizer{TAction}"/> is registered for the action's exact type, an
/// <see cref="AccessDeniedException"/> is thrown.
/// </description></item>
/// <item><description>
/// The authorizers run one after another. If any of them refuses, an <see cref="AccessDeniedException"/> is thrown.
/// </description></item>
/// </list>
/// <para>
/// The handler only runs when the action is allowed. A stream query is authorized when its results are first
/// enumerated. Messages are not authorized, because they are published by the application itself. Never publish a
/// message built directly from user input: send an authorized command instead, and publish the message from its
/// handler.
/// </para>
/// <para>
/// Register this behaviour before a validation behaviour, so that users who may not run an action cannot learn its
/// validation rules. Create it from the current scope, so that scoped services are resolved from it.
/// </para>
/// </remarks>
public sealed class AuthorizationBehavior : IBehavior
{
    private static readonly ConcurrentDictionary<Type, bool> AnonymousActions = new();

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationBehavior"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider to resolve the <see cref="IPrincipalAccessor"/> and authorizers from.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public AuthorizationBehavior(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the command.</exception>
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        IsAnonymous(command) ? next(ct) : AuthorizeThenHandleAsync(command, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the command.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default) =>
        IsAnonymous(command) ? next(ct) : AuthorizeThenHandleAsync(command, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the query.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default) =>
        IsAnonymous(query) ? next(ct) : AuthorizeThenHandleAsync(query, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">
    /// There is no signed-in user. Thrown when the results are first enumerated.
    /// </exception>
    /// <exception cref="AccessDeniedException">
    /// The user may not run the stream query. Thrown when the results are first enumerated.
    /// </exception>
    public IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        CancellationToken ct = default) =>
        IsAnonymous(query) ? next(ct) : AuthorizeThenStreamAsync(query, next, ct);

    private static bool IsAnonymous(object action) =>
        AnonymousActions.GetOrAdd(
            action.GetType(),
            static type => type.IsDefined(typeof(AllowAnonymousAccessAttribute), inherit: false));

    private async ValueTask AuthorizeThenHandleAsync(object action, Func<CancellationToken, ValueTask> next, CancellationToken ct)
    {
        await AuthorizeAsync(action, ct).ConfigureAwait(false);
        await next(ct).ConfigureAwait(false);
    }

    private async ValueTask<TResult> AuthorizeThenHandleAsync<TResult>(
        object action,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct)
    {
        await AuthorizeAsync(action, ct).ConfigureAwait(false);
        return await next(ct).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<TResult> AuthorizeThenStreamAsync<TResult>(
        object action,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await AuthorizeAsync(action, ct).ConfigureAwait(false);

        await foreach (var result in next(ct).ConfigureAwait(false))
        {
            yield return result;
        }
    }

    private ValueTask AuthorizeAsync(object action, CancellationToken ct)
    {
        var accessor = _serviceProvider.GetService(typeof(IPrincipalAccessor)) as IPrincipalAccessor
            ?? throw new InvalidOperationException(
                $"No '{typeof(IPrincipalAccessor)}' is registered. Register one that returns the current user.");

        var principal = accessor.Principal;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new AuthenticationRequiredException($"Authentication is required to run '{action.GetType()}'.");
        }

        return ActionAuthorization.Get(action.GetType()).AuthorizeAsync(action, principal, _serviceProvider, ct);
    }
}
