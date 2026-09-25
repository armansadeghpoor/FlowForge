using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Queries;

/// <summary>
/// Defines storage-agnostic queries for workflow execution visibility.
/// </summary>
public interface IExecutionQueryService
{
    /// <summary>
    /// Gets a derived summary of the current workflow execution snapshot.
    /// </summary>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The execution summary, or <see langword="null"/> when not found.</returns>
    Task<ExecutionSummary?> GetSummaryAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists derived summaries for all workflow execution snapshots.
    /// </summary>
    Task<IReadOnlyList<ExecutionSummary>> ListSummariesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds execution summaries associated with a correlation identifier.
    /// </summary>
    Task<IReadOnlyList<ExecutionSummary>> FindExecutionsByCorrelationIdAsync(
        ExecutionCorrelationId correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the ordered audit timeline for a workflow execution.
    /// </summary>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The ordered execution history.</returns>
    Task<IReadOnlyList<ExecutionHistoryEntry>> GetTimelineAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken);
}
