using FlowForge.Abstractions.Scheduling;
using FlowForge.Application.Common;

namespace FlowForge.Application.Scheduling;

/// <summary>
/// Defines explicit workflow scheduling use cases.
/// </summary>
public interface IWorkflowSchedulingService
{
    /// <summary>
    /// Evaluates and dispatches currently due workflow schedules.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<ScheduleExecutionResult>>> RunDueSchedulesAsync(
        CancellationToken cancellationToken);
}
