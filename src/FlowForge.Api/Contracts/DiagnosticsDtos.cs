namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents aggregate workflow execution metrics.
/// </summary>
public sealed record ExecutionMetricsDto
{
    public required int TotalExecutions { get; init; }

    public required int RunningExecutions { get; init; }

    public required int CompletedExecutions { get; init; }

    public required int FailedExecutions { get; init; }

    public required TimeSpan? AverageDuration { get; init; }

    public required DateTime? LastExecutionTimestamp { get; init; }
}
