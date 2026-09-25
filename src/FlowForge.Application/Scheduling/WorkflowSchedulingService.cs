using FlowForge.Abstractions.Scheduling;
using FlowForge.Application.Common;

namespace FlowForge.Application.Scheduling;

/// <summary>
/// Provides the application boundary for explicit scheduler runs.
/// </summary>
public sealed class WorkflowSchedulingService : IWorkflowSchedulingService
{
    private readonly IWorkflowScheduler _scheduler;

    /// <summary>
    /// Initializes a workflow scheduling service.
    /// </summary>
    public WorkflowSchedulingService(IWorkflowScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<ScheduleExecutionResult>>> RunDueSchedulesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _scheduler.RunDueSchedulesAsync(cancellationToken);
            return ApplicationResult<IReadOnlyList<ScheduleExecutionResult>>.Success(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<ScheduleExecutionResult>>.Failure(
                new ApplicationError(
                    "SchedulingFailed",
                    "The workflow scheduling operation failed."));
        }
    }
}
