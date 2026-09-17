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
    /// Creates a workflow execution snapshot, including its supplied node states.
    /// Existing executions are never overwritten.
    /// </summary>
    /// <param name="execution">The workflow execution to create.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">The execution identifier already exists.</exception>
    Task CreateExecutionAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the lifecycle status and timestamps of an existing workflow execution,
    /// preserving its node states. Supplied null timestamps clear the stored values.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="status">The workflow execution status.</param>
    /// <param name="startedAt">The time execution started, if applicable.</param>
    /// <param name="completedAt">The time execution completed, if applicable.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="KeyNotFoundException">The workflow execution does not exist.</exception>
    Task UpdateWorkflowStatusAsync(
        WorkflowExecutionId id,
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a node snapshot within an existing workflow execution. A save replaces
    /// the entire node state with the same node execution identifier within that workflow,
    /// or adds it when absent. The last applied save wins.
    /// </summary>
    /// <param name="executionId">The containing workflow execution identifier.</param>
    /// <param name="nodeExecution">The node execution state to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="KeyNotFoundException">The workflow execution does not exist.</exception>
    Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow execution aggregate by its identifier, including its saved node states.
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
