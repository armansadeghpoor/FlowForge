namespace FlowForge.Abstractions.Observability;

/// <summary>
/// Represents aggregated duration observations for one metric.
/// </summary>
/// <param name="Count">The number of recorded observations.</param>
/// <param name="Total">The total recorded duration.</param>
/// <param name="Average">The average recorded duration.</param>
public sealed record DurationMetricSnapshot(
    long Count,
    TimeSpan Total,
    TimeSpan Average);
