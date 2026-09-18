using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Queries;

/// <summary>
/// Represents derived visibility information for a workflow execution snapshot.
/// </summary>
public sealed record ExecutionSummary
{
    public required WorkflowExecutionId WorkflowExecutionId { get; init; }

    public required WorkflowExecutionStatus Status { get; init; }

    public required string DefinitionVersion { get; init; }

    public required DateTime? StartedAt { get; init; }

    public required DateTime? CompletedAt { get; init; }

    public required string? OwnerId { get; init; }

    public required DateTime? LastHeartbeatAt { get; init; }

    public required NodeExecutionCounts NodeExecutionCounts { get; init; }
}
