using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Executions;

/// <summary>
/// Represents a single workflow execution instance.
/// </summary>
public sealed record WorkflowExecution
{
    public required WorkflowExecutionId Id { get; init; }

    public required WorkflowId WorkflowId { get; init; }

    public required WorkflowExecutionStatus Status { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime? StartedAt { get; init; }

    public required DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the execution owner, if one has been assigned.
    /// </summary>
    public required string? OwnerId { get; init; }

    /// <summary>
    /// Gets the time of the most recent execution heartbeat, if one has been recorded.
    /// </summary>
    public required DateTime? LastHeartbeatAt { get; init; }

    public required IReadOnlyList<NodeExecutionState> Nodes { get; init; }
}
