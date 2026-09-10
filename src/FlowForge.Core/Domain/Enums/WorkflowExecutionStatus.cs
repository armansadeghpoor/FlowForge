namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes the lifecycle status of a workflow execution.
/// </summary>
public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
