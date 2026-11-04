using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Tests.Fakes;

public sealed class CommandHandler<TCommand>(Func<TCommand, CancellationToken, ValueTask> handle) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default) => handle(command, cancellationToken);
}

public sealed class CommandHandler<TCommand, TResult>(Func<TCommand, CancellationToken, ValueTask<TResult>> handle)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default) => handle(command, cancellationToken);
}

public sealed class QueryHandler<TQuery, TResult>(Func<TQuery, CancellationToken, ValueTask<TResult>> handle)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public ValueTask<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default) => handle(query, cancellationToken);
}

public sealed class MessageHandler<TMessage>(Func<TMessage, CancellationToken, ValueTask> handle) : IMessageHandler<TMessage>
    where TMessage : IMessage
{
    public ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken = default) => handle(message, cancellationToken);
}

public sealed class StreamQueryHandler<TQuery, TResult>(Func<TQuery, CancellationToken, IAsyncEnumerable<TResult>> handle)
    : IStreamQueryHandler<TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    public IAsyncEnumerable<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default) => handle(query, cancellationToken);
}
