using System;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Dispatchers;

namespace Datapoint.Mediator;

/// <summary>
/// Dispatches commands and queries to their handler, and publishes messages to their handlers, resolving handlers and
/// behaviours from an <see cref="IServiceProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Handlers and behaviours are resolved from the service provider passed to the constructor. To resolve scoped
/// handlers correctly, create the mediator from the current scope, for example by registering it as a scoped service.
/// </para>
/// <para>
/// Behaviours (<see cref="IBehavior"/>) run in registration order, with the first registered behaviour outermost.
/// A message's handlers run one after another, in registration order, and publishing stops at the first exception.
/// </para>
/// </remarks>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve handlers and behaviours from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public Mediator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the command.</exception>
    public ValueTask SendAsync(ICommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommandDispatcher.Get(command.GetType()).DispatchAsync(command, _serviceProvider, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the command.</exception>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ResultCommandDispatcher<TResult>.Get(command.GetType()).DispatchAsync(command, _serviceProvider, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the query.</exception>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return QueryDispatcher<TResult>.Get(query.GetType()).DispatchAsync(query, _serviceProvider, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public ValueTask PublishAsync(IMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return MessageDispatcher.Get(message.GetType()).DispatchAsync(message, _serviceProvider, ct);
    }
}
