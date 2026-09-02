using Prometheus;

namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public sealed class CentralSchedulerMetrics : ICentralSchedulerMetrics
{
    public static CentralSchedulerMetrics Instance { get; } = new();

    private static readonly Gauge QueueDepth = Metrics.CreateGauge(
        "ast_tracking_scheduler_queue_depth",
        "Number of rooms currently waiting in the central tracking scheduler.");

    private static readonly Gauge ActivePolls = Metrics.CreateGauge(
        "ast_tracking_scheduler_active_polls",
        "Number of room polls currently executing.");

    private static readonly Gauge OpenBreakers = Metrics.CreateGauge(
        "ast_tracking_scheduler_open_breakers",
        "Number of WebHost origin circuit breakers currently open.");

    private static readonly Histogram QueueLag = Metrics.CreateHistogram(
        "ast_tracking_scheduler_queue_lag_seconds",
        "Delay between a room due time and dispatch.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 12)
        });

    private static readonly Histogram PollDuration = Metrics.CreateHistogram(
        "ast_tracking_scheduler_poll_duration_seconds",
        "End-to-end room poll duration.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 14)
        });

    private static readonly Counter PollOutcomes = Metrics.CreateCounter(
        "ast_tracking_scheduler_poll_outcomes_total",
        "Room poll outcomes by non-sensitive classification.",
        new CounterConfiguration
        {
            LabelNames = ["outcome"]
        });

    public void SetQueueDepth(int value) => QueueDepth.Set(Math.Max(0, value));
    public void SetActivePolls(int value) => ActivePolls.Set(Math.Max(0, value));
    public void SetOpenBreakers(int value) => OpenBreakers.Set(Math.Max(0, value));

    public void ObserveQueueLag(TimeSpan lag)
        => QueueLag.Observe(Math.Max(0, lag.TotalSeconds));

    public void ObservePoll(TimeSpan duration, RoomPollResult result)
    {
        PollDuration.Observe(Math.Max(0, duration.TotalSeconds));
        var outcome = result.Success ? "success" : result.FailureKind.ToString().ToLowerInvariant();
        PollOutcomes.WithLabels(outcome).Inc();
    }
}
