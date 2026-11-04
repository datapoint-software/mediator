using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class ActionDispatcherTests
{
    private readonly List<string> _log = [];
    private readonly ActionDispatcher _dispatcher;

    public ActionDispatcherTests()
    {
        var provider = new FakeServiceProvider()
            .AddAll<IBehavior>(new LoggingBehavior("behavior", _log))
            .Add<ICommandHandler<CreateInvoice>>(new CommandHandler<CreateInvoice>((c, _) => Log($"created {c.Customer} {c.Amount}")))
            .Add<ICommandHandler<ApproveInvoice, bool>>(new CommandHandler<ApproveInvoice, bool>((c, _) => new(c.Id > 0)))
            .Add<IQueryHandler<GetInvoice, string>>(new QueryHandler<GetInvoice, string>((q, _) => new($"invoice {q.Id}")))
            .Add<IStreamQueryHandler<ListInvoices, int>>(new StreamQueryHandler<ListInvoices, int>((q, cancellationToken) => Streams.CountTo(q.Count, cancellationToken: cancellationToken)));
        var catalog = new ActionCatalog([typeof(CreateInvoice), typeof(ApproveInvoice), typeof(GetInvoice), typeof(ListInvoices)]);
        _dispatcher = new ActionDispatcher(new ServiceProviderMediator(provider), catalog);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var catalog = new ActionCatalog([]);

        Assert.Equal("mediator", Assert.Throws<ArgumentNullException>(() => new ActionDispatcher(null!, catalog)).ParamName);
        Assert.Equal("catalog", Assert.Throws<ArgumentNullException>(() => new ActionDispatcher(new ServiceProviderMediator(new FakeServiceProvider()), null!)).ParamName);
    }

    [Fact]
    public async Task DispatchAsync_Command_DeserializesWithWebDefaultsAndReturnsNull()
    {
        var result = await _dispatcher.DispatchAsync("invoice.create", Json("""{ "customer": "Ana", "amount": 100 }"""), cancellationToken: Ct);

        Assert.Null(result);
        Assert.Contains("created Ana 100", _log);
    }

    [Fact]
    public async Task DispatchAsync_CommandWithResult_ReturnsResult()
    {
        Assert.Equal(true, await _dispatcher.DispatchAsync("invoice.approve", Json("""{ "id": 7 }"""), cancellationToken: Ct));
    }

    [Fact]
    public async Task DispatchAsync_Query_ReturnsResult()
    {
        Assert.Equal("invoice 7", await _dispatcher.DispatchAsync("invoice.get", Json("""{ "id": 7 }"""), cancellationToken: Ct));
    }

    [Fact]
    public async Task StreamAsync_StreamQuery_StreamsResults()
    {
        var results = await _dispatcher.StreamAsync("invoice.list", Json("""{ "count": 3 }"""), cancellationToken: Ct).ToListAsync(Ct);

        Assert.Equal([1, 2, 3], results.Cast<int>());
    }

    [Fact]
    public async Task DispatchAsync_UsesGivenOptions()
    {
        var strict = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };

        await _dispatcher.DispatchAsync("invoice.create", Json("""{ "Customer": "Rui", "Amount": 5 }"""), strict, Ct);

        Assert.Contains("created Rui 5", _log);
    }

    [Fact]
    public async Task DispatchAsync_UndefinedArguments_AreTreatedAsEmptyObject()
    {
        Assert.Equal("invoice 0", await _dispatcher.DispatchAsync("invoice.get", default, cancellationToken: Ct));
    }

    [Fact]
    public async Task DispatchAsync_RunsBehaviors()
    {
        await _dispatcher.DispatchAsync("invoice.get", Json("""{ "id": 1 }"""), cancellationToken: Ct);

        Assert.Equal(["behavior>", "<behavior"], _log);
    }

    [Fact]
    public async Task UnknownAction_ThrowsWithoutNamingIt()
    {
        var unknown = await Assert.ThrowsAsync<ActionNotFoundException>(async () => await _dispatcher.DispatchAsync("invoice.unknown", default, cancellationToken: Ct));

        Assert.DoesNotContain("invoice", unknown.Message);
        Assert.Throws<ActionNotFoundException>(() => _dispatcher.StreamAsync("invoice.unknown", default, cancellationToken: Ct));
    }

    [Fact]
    public async Task DispatchAsync_InvalidArguments_ThrowsWithoutEchoingInput()
    {
        var exception = await Assert.ThrowsAsync<InvalidActionArgumentsException>(
            async () => await _dispatcher.DispatchAsync("invoice.create", Json("""{ "amount": "secret-input" }"""), cancellationToken: Ct));

        Assert.DoesNotContain("secret-input", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task DispatchAsync_NullArguments_Throws()
    {
        await Assert.ThrowsAsync<InvalidActionArgumentsException>(
            async () => await _dispatcher.DispatchAsync("invoice.get", Json("null"), cancellationToken: Ct));
    }

    [Fact]
    public void WrongMethodForKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _dispatcher.DispatchAsync("invoice.list", Json("{}"), cancellationToken: Ct));
        Assert.Throws<InvalidOperationException>(() => _dispatcher.StreamAsync("invoice.get", Json("{}"), cancellationToken: Ct));
    }

    [Fact]
    public void NullName_Throws()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => _dispatcher.DispatchAsync(null!, default, cancellationToken: Ct)).ParamName);
    }

    [Fact]
    public async Task DispatchAsync_WithoutOptions_DefaultsToNullOptionsForADecorator()
    {
        IActionDispatcher decorator = new OptionsRecordingDispatcher(_dispatcher);

        Assert.Equal("invoice 7", await decorator.DispatchAsync("invoice.get", Json("""{ "id": 7 }"""), Ct));
        Assert.Equal([1, 2], await decorator.StreamAsync("invoice.list", Json("""{ "count": 2 }"""), Ct).ToListAsync(Ct));
        Assert.Equal(["dispatch null", "stream null"], ((OptionsRecordingDispatcher)decorator).Calls);
    }

    [Fact]
    public void Exceptions_HaveStandardConstructors()
    {
        var inner = new InvalidOperationException();

        Assert.Equal("The requested action is not available.", new ActionNotFoundException().Message);
        Assert.Equal("message", new ActionNotFoundException("message").Message);
        Assert.Same(inner, new ActionNotFoundException("message", inner).InnerException);
        Assert.Equal("The action's arguments are not valid.", new InvalidActionArgumentsException().Message);
        Assert.Equal("message", new InvalidActionArgumentsException("message").Message);
        Assert.Same(inner, new InvalidActionArgumentsException("message", inner).InnerException);
        Assert.Null(new InvalidActionArgumentsException("message", inner).Field);
    }

    [Fact]
    public void InvalidActionArgumentsException_CarriesActionAndField()
    {
        var inner = new InvalidOperationException();

        var exception = new InvalidActionArgumentsException("message", "invoice.get", "id", inner);

        Assert.Equal("message", exception.Message);
        Assert.Equal("invoice.get", exception.ActionName);
        Assert.Equal("id", exception.Field);
        Assert.Null(exception.Reason);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void InvalidActionArgumentsException_CarriesReasonOnlyWithAField()
    {
        Assert.Equal(
            InvalidActionArgumentReason.Missing,
            new InvalidActionArgumentsException("message", "invoice.get", "id", InvalidActionArgumentReason.Missing, null).Reason);
        Assert.Null(new InvalidActionArgumentsException("message", "invoice.get", null, InvalidActionArgumentReason.Missing, null).Reason);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private ValueTask Log(string entry)
    {
        _log.Add(entry);
        return default;
    }

    [Api]
    [Action("invoice.create", "A test action.")]
    private sealed record CreateInvoice(string Customer, int Amount) : ICommand;

    [Api]
    [Action("invoice.approve", "A test action.")]
    private sealed record ApproveInvoice(int Id) : ICommand<bool>;

    [Api]
    [Action("invoice.get", "A test action.")]
    private sealed record GetInvoice(int Id) : IQuery<string>;

    [Api]
    [Action("invoice.list", "A test action.")]
    private sealed record ListInvoices(int Count) : IStreamQuery<int>;

    // Implements only the overloads with options, as an app's decorator written before the short overloads existed.
    private sealed class OptionsRecordingDispatcher(IActionDispatcher inner) : IActionDispatcher
    {
        public List<string> Calls { get; } = [];

        public ValueTask<object?> DispatchAsync(string name, JsonElement arguments, JsonSerializerOptions? options, CancellationToken cancellationToken = default)
        {
            Calls.Add($"dispatch {options?.ToString() ?? "null"}");
            return inner.DispatchAsync(name, arguments, options, cancellationToken);
        }

        public IAsyncEnumerable<object?> StreamAsync(string name, JsonElement arguments, JsonSerializerOptions? options, CancellationToken cancellationToken = default)
        {
            Calls.Add($"stream {options?.ToString() ?? "null"}");
            return inner.StreamAsync(name, arguments, options, cancellationToken);
        }
    }
}
