namespace FlowForge.Abstractions.Observability;

/// <summary>
/// Collects provider-independent operational metrics.
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Increments a named counter.
    /// </summary>
    void IncrementCounter(string name, long amount = 1);

    /// <summary>
    /// Records one duration observation for a named metric.
    /// </summary>
    void RecordDuration(string name, TimeSpan duration);

    /// <summary>
    /// Gets an immutable snapshot of the currently collected metrics.
    /// </summary>
    MetricsSnapshot GetSnapshot();
}
