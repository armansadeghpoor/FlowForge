using System.Text.Json;

namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents a workflow execution summary.
/// </summary>
public sealed record ExecutionSummaryDto
{
    public required Guid WorkflowExecutionId { get; init; }

    public required string Status { get; init; }

    public required string DefinitionVersion { get; init; }

    public required DateTime? StartedAt { get; init; }

    public required DateTime? CompletedAt { get; init; }

    public required string? OwnerId { get; init; }

    public required DateTime? LastHeartbeatAt { get; init; }

    public required NodeExecutionCountsDto NodeExecutionCounts { get; init; }
}

/// <summary>
/// Represents execution node counts grouped by lifecycle status.
/// </summary>
public sealed record NodeExecutionCountsDto
{
    public required int Total { get; init; }

    public required int Pending { get; init; }

    public required int Running { get; init; }

    public required int Succeeded { get; init; }

    public required int Failed { get; init; }

    public required int Cancelled { get; init; }
}

/// <summary>
/// Represents one execution timeline entry.
/// </summary>
public sealed record ExecutionTimelineEntryDto
{
    public required Guid Id { get; init; }

    public required Guid WorkflowExecutionId { get; init; }

    public required Guid? NodeExecutionId { get; init; }

    public required string EventType { get; init; }

    public required DateTime Timestamp { get; init; }

    public required JsonElement? Metadata { get; init; }
}
