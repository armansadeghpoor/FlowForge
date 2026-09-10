using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.State;

/// <summary>
/// Defines persistence operations for workflow execution state.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Creates a workflow execution record.
    /// </summary>
    /// <param name="execution">The workflow execution to create.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task CreateExecutionAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the lifecycle status and timestamps of a workflow execution.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="status">The workflow execution status.</param>
    /// <param name="startedAt">The time execution started, if applicable.</param>
    /// <param name="completedAt">The time execution completed, if applicable.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task UpdateWorkflowStatusAsync(
        WorkflowExecutionId id,
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves the execution state of an individual workflow node.
    /// </summary>
    /// <param name="executionId">The containing workflow execution identifier.</param>
    /// <param name="nodeExecution">The node execution state to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow execution by its identifier.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The workflow execution, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowExecution?> GetExecutionAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an individual node execution state.
    /// </summary>
    /// <param name="executionId">The containing workflow execution identifier.</param>
    /// <param name="nodeExecutionId">The node execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The node execution state, or <see langword="null"/> when it is not found.</returns>
    Task<NodeExecutionState?> GetNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionId nodeExecutionId,
        CancellationToken cancellationToken);
}
