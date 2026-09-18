namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes an auditable workflow or node execution event.
/// </summary>
public enum ExecutionHistoryEventType
{
    WorkflowCreated,
    WorkflowStarted,
    WorkflowCompleted,
    WorkflowFailed,
    NodeStarted,
    NodeCompleted,
    NodeFailed
}
