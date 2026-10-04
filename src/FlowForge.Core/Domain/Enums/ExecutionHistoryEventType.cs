namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes an auditable workflow or node execution event.
/// </summary>
public enum ExecutionHistoryEventType
{
    WorkflowCreated = 0,
    WorkflowStarted = 1,
    WorkflowCompleted = 2,
    WorkflowFailed = 3,
    NodeStarted = 4,
    NodeCompleted = 5,
    NodeFailed = 6,
    WorkflowCancelled = 7,
    NodeCancelled = 8
}
