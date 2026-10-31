using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

// Listeners are process-wide, so every assertion filters on action types used only by these tests, and the telemetry
// test classes share a collection, so they never run at the same time.
[Collection(TelemetryCollection.Name)]
public sealed class TelemetryTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ConcurrentQueue<(double Value, Dictionary<string, object?> Tags)> _measurements = new();
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public TelemetryTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediatorTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MediatorTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            _measurements.Enqueue((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        _meterListener.Start();
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    [Fact]
    public async Task SendAsync_Success_RecordsSpanAndDuration()
    {
        string? currentInHandler = null;
        var provider = new FakeServiceProvider().Add<ICommandHandler<TracedCommand>>(new CommandHandler<TracedCommand>((_, _) =>
        {
            currentInHandler = Activity.Current?.OperationName;
            return default;
        }));

        await new Mediator(provider).SendAsync(new TracedCommand(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(ActivitiesFor<TracedCommand>());
        Assert.Equal("send TracedCommand", activity.OperationName);
        Assert.Equal("send", activity.GetTagItem("datapoint.mediator.operation"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.GetTagItem("error.type"));
        Assert.Equal("send TracedCommand", currentInHandler);

        var measurement = Assert.Single(MeasurementsFor<TracedCommand>());
        Assert.Equal("send", measurement.Tags["datapoint.mediator.operation"]);
        Assert.False(measurement.Tags.ContainsKey("error.type"));
        Assert.True(measurement.Value >= 0);
    }

    [Fact]
    public async Task SendAsync_Failure_RecordsErrorTypeButNotMessage()
    {
        var provider = new FakeServiceProvider().Add<ICommandHandler<FailingCommand>>(
            new CommandHandler<FailingCommand>((_, _) => throw new InvalidOperationException("secret user input")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new Mediator(provider).SendAsync(new FailingCommand(), TestContext.Current.CancellationToken));

        var activity = Assert.Single(ActivitiesFor<FailingCommand>());
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem("error.type"));
        Assert.DoesNotContain(activity.TagObjects, t => t.Value is string s && s.Contains("secret"));
        Assert.Empty(activity.Events);
        Assert.Null(activity.StatusDescription);

        var measurement = Assert.Single(MeasurementsFor<FailingCommand>());
        Assert.Equal(typeof(InvalidOperationException).FullName, measurement.Tags["error.type"]);
    }

    [Fact]
    public async Task SendAsync_MissingHandler_RecordsFailure()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new Mediator(new FakeServiceProvider()).SendAsync(new UnhandledCommand(), TestContext.Current.CancellationToken));

        var activity = Assert.Single(ActivitiesFor<UnhandledCommand>());
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task OtherOperations_RecordTheirOperationName()
    {
        var provider = new FakeServiceProvider()
            .Add<ICommandHandler<TracedResultCommand, int>>(new CommandHandler<TracedResultCommand, int>((_, _) => new(1)))
            .Add<IQueryHandler<TracedQuery, int>>(new QueryHandler<TracedQuery, int>((_, _) => new(2)))
            .AddAll<IMessageHandler<TracedMessage>>(new MessageHandler<TracedMessage>((_, _) => default));
        var mediator = new Mediator(provider);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(1, await mediator.SendAsync(new TracedResultCommand(), ct));
        Assert.Equal(2, await mediator.QueryAsync(new TracedQuery(), ct));
        await mediator.PublishAsync(new TracedMessage(), ct);

        Assert.Equal("send TracedResultCommand", Assert.Single(ActivitiesFor<TracedResultCommand>()).OperationName);
        Assert.Equal("query TracedQuery", Assert.Single(ActivitiesFor<TracedQuery>()).OperationName);
        Assert.Equal("publish TracedMessage", Assert.Single(ActivitiesFor<TracedMessage>()).OperationName);
        Assert.Equal("query", Assert.Single(MeasurementsFor<TracedQuery>()).Tags["datapoint.mediator.operation"]);
    }

    [Fact]
    public async Task StreamAsync_RecordsSpanCoveringEnumeration()
    {
        var provider = new FakeServiceProvider().Add<IStreamQueryHandler<TracedStream, int>>(
            new StreamQueryHandler<TracedStream, int>((q, ct) => Streams.CountTo(q.Count, ct: ct)));

        var results = new Mediator(provider).StreamAsync(new TracedStream(3), TestContext.Current.CancellationToken);
        Assert.Empty(ActivitiesFor<TracedStream>());

        Assert.Equal([1, 2, 3], await results.ToListAsync(TestContext.Current.CancellationToken));

        var activity = Assert.Single(ActivitiesFor<TracedStream>());
        Assert.Equal("stream TracedStream", activity.OperationName);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Single(MeasurementsFor<TracedStream>());
    }

    [Fact]
    public void StreamAsync_MissingHandler_ThrowsImmediatelyAndRecordsFailure()
    {
        Assert.Throws<InvalidOperationException>(
            () => new Mediator(new FakeServiceProvider()).StreamAsync(new UnhandledStream(), TestContext.Current.CancellationToken));

        Assert.Equal(ActivityStatusCode.Error, Assert.Single(ActivitiesFor<UnhandledStream>()).Status);
    }

    private IEnumerable<Activity> ActivitiesFor<TAction>() =>
        _activities.Where(a => (string?)a.GetTagItem("datapoint.mediator.action") == typeof(TAction).FullName);

    private IEnumerable<(double Value, Dictionary<string, object?> Tags)> MeasurementsFor<TAction>() =>
        _measurements.Where(m => (string?)m.Tags["datapoint.mediator.action"] == typeof(TAction).FullName);

    private sealed record TracedCommand : ICommand;

    private sealed record FailingCommand : ICommand;

    private sealed record UnhandledCommand : ICommand;

    private sealed record TracedResultCommand : ICommand<int>;

    private sealed record TracedQuery : IQuery<int>;

    private sealed record TracedMessage : IMessage;

    private sealed record TracedStream(int Count) : IStreamQuery<int>;

    private sealed record UnhandledStream : IStreamQuery<int>;
}
