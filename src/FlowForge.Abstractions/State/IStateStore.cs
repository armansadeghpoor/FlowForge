using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.State;

/// <summary>
/// Defines persistence operations for workflow execution state.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Saves a workflow execution.
    /// </summary>
    /// <param name="execution">The workflow execution to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow execution by its identifier.
    /// </summary>
    /// <param name="id">The workflow execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The workflow execution, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowExecution?> GetAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken);
}
