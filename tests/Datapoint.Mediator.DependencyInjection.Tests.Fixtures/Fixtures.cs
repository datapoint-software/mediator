using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.DependencyInjection.Tests.Fixtures;

// Types for the assembly-scanning tests. Message handlers are declared out of name order on purpose,
// so the tests can check that scanning sorts them.

public sealed record FixtureCommand : ICommand;

public sealed record FixtureQuery : IQuery<int>;

public sealed record FixtureMessage : IMessage;

public sealed record SkippedCommand : ICommand;

public sealed class FixtureCommandHandler : ICommandHandler<FixtureCommand>
{
    public ValueTask HandleAsync(FixtureCommand command, CancellationToken ct = default) => default;
}

internal sealed class InternalQueryHandler : IQueryHandler<FixtureQuery, int>
{
    public ValueTask<int> HandleAsync(FixtureQuery query, CancellationToken ct = default) => new(42);
}

public sealed class ZetaMessageHandler : IMessageHandler<FixtureMessage>
{
    public ValueTask HandleAsync(FixtureMessage message, CancellationToken ct = default) => default;
}

public sealed class AlphaMessageHandler : IMessageHandler<FixtureMessage>
{
    public ValueTask HandleAsync(FixtureMessage message, CancellationToken ct = default) => default;
}

public sealed class MiddleMessageHandler : IMessageHandler<FixtureMessage>
{
    public ValueTask HandleAsync(FixtureMessage message, CancellationToken ct = default) => default;
}

// Must be skipped by scanning: abstract.
public abstract class AbstractSkippedHandler : ICommandHandler<SkippedCommand>
{
    public abstract ValueTask HandleAsync(SkippedCommand command, CancellationToken ct = default);
}

// Must be skipped by scanning: open generic.
public sealed class GenericSkippedHandler<T> : ICommandHandler<SkippedCommand>
{
    public ValueTask HandleAsync(SkippedCommand command, CancellationToken ct = default) => default;
}

// Must be skipped by scanning: behaviours are only registered explicitly.
public sealed class FixtureBehavior : IBehavior
{
    public ValueTask HandleAsync(ICommand command, Func<CancellationToken, ValueTask> next, CancellationToken ct = default) =>
        next(ct);
}
