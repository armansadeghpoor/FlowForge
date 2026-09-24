using FlowForge.Abstractions.Queries;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Queries;

/// <summary>
/// Defines application-facing workflow execution query use cases.
/// </summary>
public interface IWorkflowExecutionQueryService
{
    /// <summary>
    /// Gets an execution summary.
    /// </summary>
    Task<ApplicationResult<ExecutionSummary>> GetSummaryAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an execution audit timeline.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>> GetTimelineAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken);
}
