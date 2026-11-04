using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
public sealed class ServiceProviderMediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderMediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve handlers and behaviours from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public ServiceProviderMediator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the command.</exception>
    public ValueTask SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var dispatcher = CommandDispatcher.Get(command.GetType());
        return MediatorTelemetry.IsEnabled
            ? SendObservedAsync(dispatcher, command, cancellationToken)
            : dispatcher.DispatchAsync(command, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the command.</exception>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var dispatcher = ResultCommandDispatcher<TResult>.Get(command.GetType());
        return MediatorTelemetry.IsEnabled
            ? SendObservedAsync(dispatcher, command, cancellationToken)
            : dispatcher.DispatchAsync(command, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the query.</exception>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var dispatcher = QueryDispatcher<TResult>.Get(query.GetType());
        return MediatorTelemetry.IsEnabled
            ? QueryObservedAsync(dispatcher, query, cancellationToken)
            : dispatcher.DispatchAsync(query, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the stream query.</exception>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var dispatcher = StreamQueryDispatcher<TResult>.Get(query.GetType());
        return MediatorTelemetry.IsEnabled
            ? StreamObserved(dispatcher, query, cancellationToken)
            : dispatcher.Dispatch(query, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public ValueTask PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var dispatcher = MessageDispatcher.Get(message.GetType());
        return MediatorTelemetry.IsEnabled
            ? PublishObservedAsync(dispatcher, message, cancellationToken)
            : dispatcher.DispatchAsync(message, _serviceProvider, cancellationToken);
    }

    // The observed paths are separate methods, so the unobserved paths allocate nothing.
    private async ValueTask SendObservedAsync(CommandDispatcher dispatcher, ICommand command, CancellationToken cancellationToken)
    {
        using var scope = MediatorTelemetry.Start(MediatorTelemetry.Send, command.GetType());

        try
        {
            await dispatcher.DispatchAsync(command, _serviceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    private async ValueTask<TResult> SendObservedAsync<TResult>(
        ResultCommandDispatcher<TResult> dispatcher,
        ICommand<TResult> command,
        CancellationToken cancellationToken)
    {
        using var scope = MediatorTelemetry.Start(MediatorTelemetry.Send, command.GetType());

        try
        {
            return await dispatcher.DispatchAsync(command, _serviceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    private async ValueTask<TResult> QueryObservedAsync<TResult>(
        QueryDispatcher<TResult> dispatcher,
        IQuery<TResult> query,
        CancellationToken cancellationToken)
    {
        using var scope = MediatorTelemetry.Start(MediatorTelemetry.Query, query.GetType());

        try
        {
            return await dispatcher.DispatchAsync(query, _serviceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    private async ValueTask PublishObservedAsync(MessageDispatcher dispatcher, IMessage message, CancellationToken cancellationToken)
    {
        using var scope = MediatorTelemetry.Start(MediatorTelemetry.Publish, message.GetType());

        try
        {
            await dispatcher.DispatchAsync(message, _serviceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    // The handler is still resolved here, so a missing handler fails immediately, as it does when unobserved.
    private IAsyncEnumerable<TResult> StreamObserved<TResult>(
        StreamQueryDispatcher<TResult> dispatcher,
        IStreamQuery<TResult> query,
        CancellationToken cancellationToken)
    {
        IAsyncEnumerable<TResult> results;

        try
        {
            results = dispatcher.Dispatch(query, _serviceProvider, cancellationToken);
        }
        catch (Exception exception)
        {
            using var scope = MediatorTelemetry.Start(MediatorTelemetry.Stream, query.GetType());
            scope.Fail(exception);
            throw;
        }

        return StreamObservedAsync(results, query.GetType(), cancellationToken);
    }

    // The span covers the enumeration, from the first result requested to the last one produced.
    private static async IAsyncEnumerable<TResult> StreamObservedAsync<TResult>(
        IAsyncEnumerable<TResult> results,
        Type queryType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var scope = MediatorTelemetry.Start(MediatorTelemetry.Stream, queryType);
        var enumerator = results.GetAsyncEnumerator(cancellationToken);

        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                TResult current;

                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (Exception exception)
                {
                    scope.Fail(exception);
                    throw;
                }

                yield return current;
            }
        }
    }
}
