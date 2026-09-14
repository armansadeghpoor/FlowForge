using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Core.Domain.Executions;

/// <summary>
/// Represents the runtime state of a workflow node execution.
/// </summary>
public sealed record NodeExecutionState
{
    public required NodeExecutionId Id { get; init; }

    public required NodeId NodeId { get; init; }

    public required NodeExecutionStatus Status { get; init; }

    public required int RetryCount { get; init; }

    public required DateTime? StartedAt { get; init; }

    public required DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the optional output produced by the node execution.
    /// </summary>
    public required NodeOutput? Output { get; init; }

    public required string? ErrorMessage { get; init; }
}
