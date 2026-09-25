using FlowForge.Abstractions.Queries;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Queries;

/// <summary>
/// Provides an application result boundary over execution visibility queries.
/// </summary>
public sealed class WorkflowExecutionQueryService : IWorkflowExecutionQueryService
{
    private readonly IExecutionQueryService _queryService;

    /// <summary>
    /// Initializes a workflow execution query application service.
    /// </summary>
    public WorkflowExecutionQueryService(IExecutionQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<ExecutionSummary>> GetSummaryAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        if (executionId.Value == Guid.Empty)
        {
            return ApplicationResult<ExecutionSummary>.Failure(
                new ApplicationError(
                    "ExecutionIdRequired",
                    "A workflow execution identifier is required."));
        }

        try
        {
            var summary = await _queryService.GetSummaryAsync(
                executionId,
                cancellationToken);
            if (summary is null)
            {
                return ApplicationResult<ExecutionSummary>.Failure(
                    new ApplicationError(
                        "ExecutionNotFound",
                        "The workflow execution was not found."));
            }

            return ApplicationResult<ExecutionSummary>.Success(summary);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<ExecutionSummary>.Failure(QueryError());
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>> GetTimelineAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        if (executionId.Value == Guid.Empty)
        {
            return ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>.Failure(
                new ApplicationError(
                    "ExecutionIdRequired",
                    "A workflow execution identifier is required."));
        }

        try
        {
            var timeline = await _queryService.GetTimelineAsync(
                executionId,
                cancellationToken);
            return ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>.Success(
                timeline);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>.Failure(
                QueryError());
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<ExecutionSummary>>> FindExecutionsByCorrelationIdAsync(
        ExecutionCorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (correlationId.Value == Guid.Empty)
        {
            return ApplicationResult<IReadOnlyList<ExecutionSummary>>.Failure(
                new ApplicationError(
                    "CorrelationIdRequired",
                    "An execution correlation identifier is required."));
        }

        try
        {
            var summaries = await _queryService.FindExecutionsByCorrelationIdAsync(
                correlationId,
                cancellationToken);
            return ApplicationResult<IReadOnlyList<ExecutionSummary>>.Success(summaries);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<ExecutionSummary>>.Failure(QueryError());
        }
    }

    private static ApplicationError QueryError() =>
        new("ExecutionQueryFailed", "The workflow execution query failed.");
}
