using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Datapoint.Mediator.Benchmarks;

// Measures the cost the mediator adds on top of calling a handler directly.
// Each mediator is resolved once from a scope, so the numbers exclude scope creation.
[MemoryDiagnoser]
public class DispatchBenchmarks
{
    private static readonly Ping Ping = new();
    private static readonly ValidatedPing ValidatedPing = new("Ana");
    private static readonly Add Add = new(1, 2);
    private static readonly Echo Echo = new("value");
    private static readonly Notified Notified = new();
    private static readonly CountTo CountToTen = new(10);

    private readonly PingHandler _handler = new();
    private readonly CountToHandler _streamHandler = new();

    private ServiceProvider _plainProvider = null!;
    private ServiceProvider _behaviorsProvider = null!;
    private ServiceProvider _validationProvider = null!;
    private IServiceScope _plainScope = null!;
    private IServiceScope _behaviorsScope = null!;
    private IServiceScope _validationScope = null!;
    private IMediator _plain = null!;
    private IMediator _withBehaviors = null!;
    private IMediator _withValidation = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_plainProvider, _plainScope, _plain) = Create(o => o
            .AddHandler<PingHandler>()
            .AddHandler<AddHandler>()
            .AddHandler<EchoHandler>()
            .AddHandler<FirstNotifiedHandler>()
            .AddHandler<SecondNotifiedHandler>()
            .AddHandler<ThirdNotifiedHandler>()
            .AddHandler<CountToHandler>());

        (_behaviorsProvider, _behaviorsScope, _withBehaviors) = Create(o => o
            .AddHandler<PingHandler>()
            .AddBehavior<FirstPassThroughBehavior>()
            .AddBehavior<SecondPassThroughBehavior>());

        (_validationProvider, _validationScope, _withValidation) = Create(
            o => o.AddHandler<ValidatedPingHandler>().AddValidationBehavior(),
            s => s.AddScoped<IValidator<ValidatedPing>, ValidatedPingValidator>());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _plainScope.Dispose();
        _behaviorsScope.Dispose();
        _validationScope.Dispose();
        _plainProvider.Dispose();
        _behaviorsProvider.Dispose();
        _validationProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ValueTask DirectHandlerCall() => _handler.HandleAsync(Ping);

    [Benchmark]
    public ValueTask SendCommand() => _plain.SendAsync(Ping);

    [Benchmark]
    public ValueTask SendCommandWithTwoBehaviors() => _withBehaviors.SendAsync(Ping);

    [Benchmark]
    public ValueTask<int> SendCommandWithResult() => _plain.SendAsync(Add);

    [Benchmark]
    public ValueTask<string> Query() => _plain.QueryAsync(Echo);

    [Benchmark]
    public ValueTask PublishToThreeHandlers() => _plain.PublishAsync(Notified);

    [Benchmark]
    public async Task<int> DirectStreamHandlerTenItems()
    {
        var sum = 0;

        await foreach (var i in _streamHandler.HandleAsync(CountToTen))
        {
            sum += i;
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> StreamQueryTenItems()
    {
        var sum = 0;

        await foreach (var i in _plain.StreamAsync(CountToTen))
        {
            sum += i;
        }

        return sum;
    }

    [Benchmark]
    public ValueTask SendCommandWithValidation() => _withValidation.SendAsync(ValidatedPing);

    private static (ServiceProvider, IServiceScope, IMediator) Create(
        Action<MediatorOptions> configure,
        Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        var provider = collection.AddMediator(configure).BuildServiceProvider();
        var scope = provider.CreateScope();
        return (provider, scope, scope.ServiceProvider.GetRequiredService<IMediator>());
    }
}
