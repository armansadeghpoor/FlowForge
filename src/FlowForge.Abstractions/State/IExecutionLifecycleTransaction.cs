using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;

namespace FlowForge.Abstractions.State;

/// <summary>
/// Provides writes scoped to one workflow execution and one lifecycle transition.
/// The handle is valid only inside the store's lifecycle callback. Await each write
/// sequentially and allow failures to propagate; never run node behavior in this scope.
/// </summary>
public interface IExecutionLifecycleTransaction
{
    /// <summary>Creates the execution, rejecting an existing identifier.</summary>
    Task CreateExecutionAsync(WorkflowExecution execution, CancellationToken cancellationToken);

    /// <summary>Replaces workflow lifecycle fields, preserving all other state.</summary>
    Task UpdateWorkflowStatusAsync(
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken);

    /// <summary>Saves a node snapshot within the scoped execution.</summary>
    Task SaveNodeExecutionAsync(NodeExecutionState nodeExecution, CancellationToken cancellationToken);

    /// <summary>Appends history for the scoped execution.</summary>
    Task AppendExecutionHistoryAsync(ExecutionHistoryEntry entry, CancellationToken cancellationToken);
}
