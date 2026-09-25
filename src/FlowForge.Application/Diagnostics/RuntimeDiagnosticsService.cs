using FlowForge.Abstractions.Queries;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Enums;

namespace FlowForge.Application.Diagnostics;

/// <summary>
/// Calculates runtime diagnostics from execution query summaries.
/// </summary>
public sealed class RuntimeDiagnosticsService : IRuntimeDiagnosticsService
{
    private readonly IExecutionQueryService _executionQueryService;

    /// <summary>
    /// Initializes a new runtime diagnostics service.
    /// </summary>
    /// <param name="executionQueryService">The execution visibility query service.</param>
    public RuntimeDiagnosticsService(IExecutionQueryService executionQueryService)
    {
        ArgumentNullException.ThrowIfNull(executionQueryService);
        _executionQueryService = executionQueryService;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<ExecutionMetricsSnapshot>> GetExecutionMetricsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var summaries = await _executionQueryService.ListSummariesAsync(cancellationToken);
            var durations = summaries
                .Where(summary => summary.StartedAt is not null && summary.CompletedAt is not null)
                .Select(summary => summary.CompletedAt!.Value - summary.StartedAt!.Value)
                .ToArray();
            var snapshot = new ExecutionMetricsSnapshot
            {
                TotalExecutions = summaries.Count,
                RunningExecutions = summaries.Count(
                    summary => summary.Status == WorkflowExecutionStatus.Running),
                CompletedExecutions = summaries.Count(
                    summary => summary.Status == WorkflowExecutionStatus.Succeeded),
                FailedExecutions = summaries.Count(
                    summary => summary.Status == WorkflowExecutionStatus.Failed),
                AverageDuration = durations.Length == 0
                    ? null
                    : TimeSpan.FromTicks((long)durations.Average(duration => duration.Ticks)),
                LastExecutionTimestamp = summaries
                    .Where(summary => summary.StartedAt is not null)
                    .Select(summary => summary.StartedAt)
                    .Max()
            };

            return ApplicationResult<ExecutionMetricsSnapshot>.Success(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<ExecutionMetricsSnapshot>.Failure(
                new ApplicationError(
                    "ExecutionMetricsQueryFailed",
                    "Execution metrics could not be retrieved."));
        }
    }
}
