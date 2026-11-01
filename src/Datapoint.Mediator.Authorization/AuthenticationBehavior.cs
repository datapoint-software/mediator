using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Refuses commands, queries and stream queries when there is no signed-in user, before anything else runs.
/// </summary>
/// <remarks>
/// <para>
/// Register it first, before the transaction behaviour, when authorization runs inside the transaction: callers who
/// aren't signed in are then refused before a transaction begins or an initializer runs. It only checks that there is
/// an authenticated user; <see cref="AuthorizationBehavior"/> still decides what the user may do, and checks
/// authentication again.
/// </para>
/// <para>
/// Actions marked with <see cref="AllowAnonymousAccessAttribute"/> and messages are not checked. When there is no user,
/// every registered <see cref="IAuthorizationObserver"/> is told, with the outcome
/// <see cref="AuthorizationOutcome.AuthenticationRequired"/>, and an <see cref="AuthenticationRequiredException"/> is
/// thrown. A stream query is checked when its results are first enumerated.
/// </para>
/// </remarks>
public sealed class AuthenticationBehavior : IBehavior
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationBehavior"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve the <see cref="IPrincipalAccessor"/> and observers from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public AuthenticationBehavior(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        Authentication.IsAnonymous(command.GetType()) ? next(ct) : AuthenticateThenHandleAsync(command, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default) =>
        Authentication.IsAnonymous(command.GetType()) ? next(ct) : AuthenticateThenHandleAsync(command, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct = default) =>
        Authentication.IsAnonymous(query.GetType()) ? next(ct) : AuthenticateThenHandleAsync(query, next, ct);

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">
    /// There is no signed-in user. Thrown when the results are first enumerated.
    /// </exception>
    public IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        CancellationToken ct = default) =>
        Authentication.IsAnonymous(query.GetType()) ? next(ct) : AuthenticateThenStreamAsync(query, next, ct);

    private async ValueTask AuthenticateThenHandleAsync(object action, Func<CancellationToken, ValueTask> next, CancellationToken ct)
    {
        await AuthenticateAsync(action, ct).ConfigureAwait(false);
        await next(ct).ConfigureAwait(false);
    }

    private async ValueTask<TResult> AuthenticateThenHandleAsync<TResult>(
        object action,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken ct)
    {
        await AuthenticateAsync(action, ct).ConfigureAwait(false);
        return await next(ct).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<TResult> AuthenticateThenStreamAsync<TResult>(
        object action,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await AuthenticateAsync(action, ct).ConfigureAwait(false);

        await foreach (var result in next(ct).ConfigureAwait(false))
        {
            yield return result;
        }
    }

    private async ValueTask AuthenticateAsync(object action, CancellationToken ct) =>
        await Authentication.RequirePrincipalAsync(_serviceProvider, action, Authentication.GetObservers(_serviceProvider), ct)
            .ConfigureAwait(false);
}
