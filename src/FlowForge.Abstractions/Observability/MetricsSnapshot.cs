using System.Collections.Frozen;

namespace FlowForge.Abstractions.Observability;

/// <summary>
/// Represents an immutable point-in-time view of operational metrics.
/// </summary>
public sealed record MetricsSnapshot
{
    /// <summary>
    /// Initializes a metrics snapshot.
    /// </summary>
    public MetricsSnapshot(
        IEnumerable<KeyValuePair<string, long>> counters,
        IEnumerable<KeyValuePair<string, DurationMetricSnapshot>> durations)
    {
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(durations);

        Counters = counters.ToFrozenDictionary(StringComparer.Ordinal);
        Durations = durations.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets counter values keyed by metric name.
    /// </summary>
    public IReadOnlyDictionary<string, long> Counters { get; }

    /// <summary>
    /// Gets aggregated durations keyed by metric name.
    /// </summary>
    public IReadOnlyDictionary<string, DurationMetricSnapshot> Durations { get; }
}
