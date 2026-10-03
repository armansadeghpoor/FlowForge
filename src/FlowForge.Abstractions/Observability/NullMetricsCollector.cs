namespace FlowForge.Abstractions.Observability;

/// <summary>
/// Provides a no-op metrics collector for hosts that do not configure metrics.
/// </summary>
public sealed class NullMetricsCollector : IMetricsCollector
{
    private static readonly MetricsSnapshot EmptySnapshot = new(
        Array.Empty<KeyValuePair<string, long>>(),
        Array.Empty<KeyValuePair<string, DurationMetricSnapshot>>());

    private NullMetricsCollector()
    {
    }

    /// <summary>Gets the shared no-op collector.</summary>
    public static NullMetricsCollector Instance { get; } = new();

    /// <inheritdoc />
    public void IncrementCounter(string name, long amount = 1)
    {
    }

    /// <inheritdoc />
    public void RecordDuration(string name, TimeSpan duration)
    {
    }

    /// <inheritdoc />
    public MetricsSnapshot GetSnapshot() => EmptySnapshot;
}
