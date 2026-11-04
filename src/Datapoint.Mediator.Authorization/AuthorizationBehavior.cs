using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Claims;
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
/// The authorizers run one after another, and stop at the first denial. If any of them denies the action, or returns
/// <see langword="null"/>, an <see cref="AccessDeniedException"/> is thrown, without the decision's details. If one
/// throws, its exception propagates.
/// </description></item>
/// <item><description>
/// Every registered <see cref="IAuthorizationObserver"/> is told the outcome, allowed or not, before the action runs
/// or the exception is thrown. If an observer throws, the action does not run.
/// </description></item>
/// </list>
/// <para>
/// The handler only runs when the action is allowed, and can read the decision from <see cref="IAuthorizationContext"/>.
/// A denial throws the exception the decision carries, if any, instead of <see cref="AccessDeniedException"/>.
/// A stream query is authorized when its results are first
/// enumerated. Messages are not authorized, because they are published by the application itself, and their handlers
/// don't see the publisher's decision. Never publish a
/// message built directly from user input: send an authorized command instead, and publish the message from its
/// handler.
/// </para>
/// <para>
/// Register this behaviour before a validation behaviour, so that users who may not run an action cannot learn its
/// validation rules. To authorize commands inside their database transaction, register it after the transaction
/// behaviour. Create it from the current scope, so that scoped services are resolved from it.
/// </para>
/// </remarks>
public sealed class AuthorizationBehavior : IBehavior
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthorizationBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationBehavior"/> class, with the default settings.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider to resolve the <see cref="IPrincipalAccessor"/> and authorizers from.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public AuthorizationBehavior(IServiceProvider serviceProvider)
        : this(serviceProvider, new AuthorizationBehaviorOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationBehavior"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider to resolve the <see cref="IPrincipalAccessor"/>, authorizers, observers and
    /// <see cref="ITransactionContext"/> from.
    /// </param>
    /// <param name="options">The settings. They are copied.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceProvider"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public AuthorizationBehavior(IServiceProvider serviceProvider, AuthorizationBehaviorOptions options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);
        _serviceProvider = serviceProvider;
        _options = options.Copy();
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the command.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="command"/> or <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The action isn't anonymous and no <see cref="IPrincipalAccessor"/> is registered, or
    /// <see cref="AuthorizationBehaviorOptions.RequireTransaction"/> requires a transaction for the action, and none is
    /// active.
    /// </exception>
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        return IsAnonymous(command) ? HandleAnonymousAsync(next, cancellationToken) : AuthorizeThenHandleAsync(command, next, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the command.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="command"/> or <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The action isn't anonymous and no <see cref="IPrincipalAccessor"/> is registered, or
    /// <see cref="AuthorizationBehaviorOptions.RequireTransaction"/> requires a transaction for the action, and none is
    /// active.
    /// </exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        ICommand<TResult> command,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        return IsAnonymous(command) ? HandleAnonymousAsync(next, cancellationToken) : AuthorizeThenHandleAsync(command, isCommand: true, next, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">There is no signed-in user.</exception>
    /// <exception cref="AccessDeniedException">The user may not run the query.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> or <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The action isn't anonymous and no <see cref="IPrincipalAccessor"/> is registered, or
    /// <see cref="AuthorizationBehaviorOptions.RequireTransaction"/> requires a transaction for the action, and none is
    /// active.
    /// </exception>
    public ValueTask<TResult> HandleAsync<TResult>(
        IQuery<TResult> query,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(next);

        return IsAnonymous(query) ? HandleAnonymousAsync(next, cancellationToken) : AuthorizeThenHandleAsync(query, isCommand: false, next, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="AuthenticationRequiredException">
    /// There is no signed-in user. Thrown when the results are first enumerated.
    /// </exception>
    /// <exception cref="AccessDeniedException">
    /// The user may not run the stream query. Thrown when the results are first enumerated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> or <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The action isn't anonymous and no <see cref="IPrincipalAccessor"/> is registered, or
    /// <see cref="AuthorizationBehaviorOptions.RequireTransaction"/> requires a transaction for the action, and none is
    /// active. Thrown when the results are first enumerated.
    /// </exception>
    public IAsyncEnumerable<TResult> HandleAsync<TResult>(
        IStreamQuery<TResult> query,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(next);

        if (!IsAnonymous(query))
        {
            return AuthorizeThenStreamAsync(query, next, cancellationToken);
        }

        // An anonymous stream query, sent from an authorized action, must not see its decision.
        if (AuthorizationContext.Result is null)
        {
            return next(cancellationToken);
        }

        return AuthorizeThenStreamAsync(null, next, cancellationToken);
    }

    /// <summary>
    /// Runs the message handlers without the decision of the action that published the message: messages are not
    /// authorized, so <see cref="IAuthorizationContext.Current"/> is <see langword="null"/> while they run.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="next">Runs the message handlers.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the message handlers have run.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public ValueTask HandleAsync(IMessage message, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(next);

        return HandleAnonymousAsync(next, cancellationToken);
    }

    private static bool IsAnonymous(object action) => AuthorizationSteps.IsAnonymous(action.GetType());

    // An anonymous action or a message, sent from an authorized action, must not see its decision.
    private static ValueTask HandleAnonymousAsync(Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken) =>
        AuthorizationContext.Result is null ? next(cancellationToken) : HandleWithoutDecisionAsync(next, cancellationToken);

    private static ValueTask<TResult> HandleAnonymousAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> next, CancellationToken cancellationToken) =>
        AuthorizationContext.Result is null ? next(cancellationToken) : HandleWithoutDecisionAsync(next, cancellationToken);

    // The decision set in an async method flows into next, and is restored for the caller when the method returns.
    private static async ValueTask HandleWithoutDecisionAsync(Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        AuthorizationContext.Result = null;
        await next(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<TResult> HandleWithoutDecisionAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken)
    {
        AuthorizationContext.Result = null;
        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask AuthorizeThenHandleAsync(object action, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        AuthorizationContext.Result = await AuthorizeAsync(action, isCommand: true, cancellationToken).ConfigureAwait(false);
        await next(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TResult> AuthorizeThenHandleAsync<TResult>(
        object action,
        bool isCommand,
        Func<CancellationToken, ValueTask<TResult>> next,
        CancellationToken cancellationToken)
    {
        AuthorizationContext.Result = await AuthorizeAsync(action, isCommand, cancellationToken).ConfigureAwait(false);
        return await next(cancellationToken).ConfigureAwait(false);
    }

    // Authorizes the stream query when one is passed; an anonymous one streams without a decision.
    private async IAsyncEnumerable<TResult> AuthorizeThenStreamAsync<TResult>(
        object? action,
        Func<CancellationToken, IAsyncEnumerable<TResult>> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var result = action is null ? null : await AuthorizeAsync(action, isCommand: false, cancellationToken).ConfigureAwait(false);

        AuthorizationContext.Result = result;
        await using var results = next(cancellationToken).ConfigureAwait(false).GetAsyncEnumerator();

        while (true)
        {
            // An async iterator restores its consumer's context on every MoveNextAsync, so set the decision each time.
            AuthorizationContext.Result = result;

            if (!await results.MoveNextAsync())
            {
                yield break;
            }

            yield return results.Current;
        }
    }

    private async ValueTask<AuthorizationResult> AuthorizeAsync(object action, bool isCommand, CancellationToken cancellationToken)
    {
        RequireTransaction(action, isCommand);

        var observers = AuthorizationSteps.GetObservers(_serviceProvider);
        var principal = await AuthorizationSteps.RequirePrincipalAsync(_serviceProvider, action, observers, cancellationToken).ConfigureAwait(false);
        var decisions = new List<AuthorizerDecision>();

        try
        {
            await ActionAuthorization.Get(action.GetType())
                .AuthorizeAsync(action, principal, _serviceProvider, decisions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (observers.Length > 0)
        {
            // The authorizer's exception propagates unchanged, so a bug isn't hidden as a denial.
            var failed = new AuthorizationResult(action, decisions, failed: true);
            await AuthorizationSteps.ObserveAsync(observers, action, principal, AuthorizationOutcome.Failed, failed, exception, cancellationToken)
                .ConfigureAwait(false);

            throw;
        }

        var result = new AuthorizationResult(action, decisions);
        var outcome = result.IsAllowed ? AuthorizationOutcome.Allowed : AuthorizationOutcome.Denied;
        await AuthorizationSteps.ObserveAsync(observers, action, principal, outcome, result, null, cancellationToken).ConfigureAwait(false);

        // Fail closed: an action that nobody wrote a rule for is not allowed. The message never includes the details.
        if (decisions.Count == 0)
        {
            throw new AccessDeniedException($"No authorizer is registered for '{action.GetType()}', so access is denied.");
        }

        if (!result.IsAllowed)
        {
            // The authorizers stop at the first denial, so it is the last decision.
            throw decisions[^1].Decision.Exception
                ?? new AccessDeniedException($"Access to '{action.GetType()}' is denied.");
        }

        return result;
    }

    // A configuration mistake, such as the transaction behaviour registered after this one, not a denial.
    private void RequireTransaction(object action, bool isCommand)
    {
        var required = _options.RequireTransaction switch
        {
            TransactionRequirement.Commands => isCommand,
            TransactionRequirement.CommandsAndQueries => true,
            _ => false,
        };

        if (required && _serviceProvider.GetService(typeof(ITransactionContext)) is not ITransactionContext { IsActive: true })
        {
            throw new InvalidOperationException(
                $"'{action.GetType()}' must be authorized inside a transaction, but none is active. Register the " +
                "transaction behaviour before the authorization behaviour, register ITransactionContext, and run " +
                "queries in transactions if they're required too.");
        }
    }
}
