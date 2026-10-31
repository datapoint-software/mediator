using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

// Applications often collect only metrics, or only traces.
[Collection(TelemetryCollection.Name)]
public sealed class MetricsOnlyTelemetryTests : IDisposable
{
    private readonly ConcurrentQueue<(double Value, Dictionary<string, object?> Tags)> _measurements = new();
    private readonly MeterListener _listener = new();

    public MetricsOnlyTelemetryTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MediatorTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            _measurements.Enqueue((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task Success_RecordsDurationWithoutSpan()
    {
        Activity? currentInHandler = null;
        var provider = new FakeServiceProvider().Add<ICommandHandler<MeteredCommand>>(new CommandHandler<MeteredCommand>((_, _) =>
        {
            currentInHandler = Activity.Current;
            return default;
        }));

        await new Mediator(provider).SendAsync(new MeteredCommand(), TestContext.Current.CancellationToken);

        Assert.Null(currentInHandler);
        var measurement = Assert.Single(MeasurementsFor<MeteredCommand>());
        Assert.False(measurement.Tags.ContainsKey("error.type"));
    }

    [Fact]
    public async Task Failure_RecordsErrorTypeWithoutSpan()
    {
        var provider = new FakeServiceProvider().Add<ICommandHandler<FailingMeteredCommand>>(
            new CommandHandler<FailingMeteredCommand>((_, _) => throw new InvalidOperationException()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new Mediator(provider).SendAsync(new FailingMeteredCommand(), TestContext.Current.CancellationToken));

        Assert.Equal(typeof(InvalidOperationException).FullName, Assert.Single(MeasurementsFor<FailingMeteredCommand>()).Tags["error.type"]);
    }

    private IEnumerable<(double Value, Dictionary<string, object?> Tags)> MeasurementsFor<TAction>() =>
        _measurements.Where(m => (string?)m.Tags["datapoint.mediator.action"] == typeof(TAction).FullName);

    private sealed record MeteredCommand : ICommand;

    private sealed record FailingMeteredCommand : ICommand;
}

[Collection(TelemetryCollection.Name)]
public sealed class TracingOnlyTelemetryTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ActivityListener _listener;

    public TracingOnlyTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediatorTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task Success_RecordsSpan()
    {
        var provider = new FakeServiceProvider().Add<ICommandHandler<TracedOnlyCommand>>(
            new CommandHandler<TracedOnlyCommand>((_, _) => default));

        await new Mediator(provider).SendAsync(new TracedOnlyCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(ActivitiesFor<TracedOnlyCommand>()).Status);
    }

    [Fact]
    public async Task Failure_RecordsErrorSpan()
    {
        var provider = new FakeServiceProvider().Add<ICommandHandler<FailingTracedOnlyCommand>>(
            new CommandHandler<FailingTracedOnlyCommand>((_, _) => throw new InvalidOperationException()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new Mediator(provider).SendAsync(new FailingTracedOnlyCommand(), TestContext.Current.CancellationToken));

        var activity = Assert.Single(ActivitiesFor<FailingTracedOnlyCommand>());
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem("error.type"));
    }

    private IEnumerable<Activity> ActivitiesFor<TAction>() =>
        _activities.Where(a => (string?)a.GetTagItem("datapoint.mediator.action") == typeof(TAction).FullName);

    private sealed record TracedOnlyCommand : ICommand;

    private sealed record FailingTracedOnlyCommand : ICommand;
}
