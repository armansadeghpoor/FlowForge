using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Executions;

/// <summary>
/// Represents a single workflow execution instance.
/// </summary>
public sealed record WorkflowExecution
{
    public required WorkflowExecutionId Id { get; init; }

    public required WorkflowId WorkflowId { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required IReadOnlyList<NodeExecutionState> Nodes { get; init; }
}
