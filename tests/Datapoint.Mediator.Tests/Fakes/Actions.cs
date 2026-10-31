namespace Datapoint.Mediator.Tests.Fakes;

public sealed record Ping : ICommand;

public sealed record Add(int A, int B) : ICommand<int>;

public sealed record Echo(string Value) : IQuery<string>;

public sealed record Notified : IMessage;

// Implements the same generic interface twice, to check that each result type reaches its own handler.
public sealed record DualCommand : ICommand<int>, ICommand<string>;

public sealed record DualQuery : IQuery<int>, IQuery<string>;
