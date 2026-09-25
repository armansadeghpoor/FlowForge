namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents process-level runtime diagnostics.
/// </summary>
public sealed record RuntimeDiagnosticsDto
{
    public required string ApplicationName { get; init; }

    public required string ApplicationVersion { get; init; }

    public required string Environment { get; init; }

    public required TimeSpan Uptime { get; init; }

    public required OperationalMetricsDto Metrics { get; init; }
}

/// <summary>
/// Represents process-local operational metrics.
/// </summary>
public sealed record OperationalMetricsDto
{
    public required IReadOnlyDictionary<string, long> Counters { get; init; }

    public required IReadOnlyDictionary<string, DurationMetricDto> Durations { get; init; }
}

/// <summary>
/// Represents aggregated duration measurements.
/// </summary>
public sealed record DurationMetricDto
{
    public required long Count { get; init; }

    public required TimeSpan Total { get; init; }

    public required TimeSpan Average { get; init; }
}
