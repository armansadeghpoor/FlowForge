namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes the lifecycle status of a node execution.
/// </summary>
public enum NodeExecutionStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
