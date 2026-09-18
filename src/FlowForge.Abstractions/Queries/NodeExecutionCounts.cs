namespace FlowForge.Abstractions.Queries;

/// <summary>
/// Represents node execution counts grouped by current lifecycle status.
/// </summary>
public sealed record NodeExecutionCounts
{
    public required int Total { get; init; }

    public required int Pending { get; init; }

    public required int Running { get; init; }

    public required int Succeeded { get; init; }

    public required int Failed { get; init; }

    public required int Cancelled { get; init; }
}
