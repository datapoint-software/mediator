using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Datapoint.Mediator;

/// <summary>
/// The names of the tracing source and meter that the mediator reports to.
/// </summary>
/// <remarks>
/// <para>
/// Every dispatch produces a span named after the operation and the action type, such as <c>send CreateUser</c>, and a
/// measurement of the <c>datapoint.mediator.dispatch.duration</c> histogram, in seconds. Both are tagged with
/// <c>datapoint.mediator.operation</c> (<c>send</c>, <c>query</c>, <c>stream</c> or <c>publish</c>),
/// <c>datapoint.mediator.action</c> (the action's full type name) and, on failure, <c>error.type</c> (the exception's
/// full type name).
/// </para>
/// <para>
/// Exception messages are not recorded, because they can contain user input or personal data.
/// </para>
/// <para>
/// To collect them with OpenTelemetry, call <c>AddSource(MediatorTelemetry.SourceName)</c> on the tracer provider
/// builder and <c>AddMeter(MediatorTelemetry.MeterName)</c> on the meter provider builder. When nothing listens, the
/// mediator adds no overhead.
/// </para>
/// </remarks>
public static class MediatorTelemetry
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> that the mediator reports spans to.
    /// </summary>
    public const string SourceName = "Datapoint.Mediator";

    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that the mediator reports measurements to.
    /// </summary>
    public const string MeterName = "Datapoint.Mediator";

    internal const string Send = "send";
    internal const string Query = "query";
    internal const string Stream = "stream";
    internal const string Publish = "publish";

    private static readonly string? Version =
        typeof(MediatorTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    internal static readonly ActivitySource Source = new(SourceName, Version);

    private static readonly Meter Meter = new(MeterName, Version);

    // The same bucket boundaries, in seconds, that .NET uses for its HTTP request durations.
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram(
        "datapoint.mediator.dispatch.duration",
        unit: "s",
        description: "The duration of mediator dispatches.",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
        });

    internal static bool IsEnabled => Source.HasListeners() || Duration.Enabled;

    internal static DispatchScope Start(string operation, Type actionType) => new(operation, actionType);

    // Records one dispatch: a span, while it is open, and its duration when disposed.
    internal sealed class DispatchScope : IDisposable
    {
        private readonly string _operation;
        private readonly Type _actionType;
        private readonly long _startTimestamp = Stopwatch.GetTimestamp();
        private readonly Activity? _activity;
        private string? _errorType;

        public DispatchScope(string operation, Type actionType)
        {
            _operation = operation;
            _actionType = actionType;
            _activity = Source.StartActivity($"{operation} {actionType.Name}", ActivityKind.Internal);
            _activity?.SetTag("datapoint.mediator.operation", operation);
            _activity?.SetTag("datapoint.mediator.action", actionType.FullName);
        }

        public void Fail(Exception exception)
        {
            _errorType = exception.GetType().FullName;
            _activity?.SetStatus(ActivityStatusCode.Error);
            _activity?.SetTag("error.type", _errorType);
        }

        public void Dispose()
        {
            _activity?.Dispose();

            if (Duration.Enabled)
            {
                var tags = new TagList
                {
                    { "datapoint.mediator.operation", _operation },
                    { "datapoint.mediator.action", _actionType.FullName },
                };

                if (_errorType is not null)
                {
                    tags.Add("error.type", _errorType);
                }

                Duration.Record(Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds, tags);
            }
        }
    }
}
