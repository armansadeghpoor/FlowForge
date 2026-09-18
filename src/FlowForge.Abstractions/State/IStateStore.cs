using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;
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
    /// Updates the most recent heartbeat timestamp of an existing workflow execution,
    /// preserving all other execution data.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="lastHeartbeatAt">The time of the most recent heartbeat.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="KeyNotFoundException">The workflow execution does not exist.</exception>
    Task UpdateHeartbeatAsync(
        WorkflowExecutionId id,
        DateTime lastHeartbeatAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds running workflow execution aggregates whose heartbeat is absent or older
    /// than the supplied threshold.
    /// </summary>
    /// <param name="threshold">The exclusive heartbeat freshness threshold.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The complete stale workflow execution aggregates.</returns>
    Task<IReadOnlyList<WorkflowExecution>> FindStaleExecutionsAsync(
        DateTime threshold,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to assign an owner to an unowned running workflow execution.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="ownerId">The owner identifier to assign.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when ownership is acquired; otherwise,
    /// <see langword="false"/> when the execution is already owned or is not running.
    /// </returns>
    /// <exception cref="KeyNotFoundException">The workflow execution does not exist.</exception>
    Task<bool> TryClaimExecutionAsync(
        WorkflowExecutionId id,
        string ownerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends an immutable audit entry to workflow execution history.
    /// Existing history is never replaced.
    /// </summary>
    /// <param name="entry">The history entry to append.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="KeyNotFoundException">The workflow execution does not exist.</exception>
    Task AppendExecutionHistoryAsync(
        ExecutionHistoryEntry entry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets workflow execution history in timestamp order, preserving append order
    /// for entries with the same timestamp.
    /// </summary>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The ordered history, or an empty collection when no history exists.</returns>
    Task<IReadOnlyList<ExecutionHistoryEntry>> GetExecutionHistoryAsync(
        WorkflowExecutionId executionId,
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
