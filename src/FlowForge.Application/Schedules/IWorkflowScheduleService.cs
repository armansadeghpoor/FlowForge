using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Application.Schedules;

/// <summary>
/// Defines workflow schedule management use cases.
/// </summary>
public interface IWorkflowScheduleService
{
    /// <summary>
    /// Validates and creates a workflow schedule.
    /// </summary>
    Task<ApplicationResult<WorkflowSchedule>> CreateAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow schedule.
    /// </summary>
    Task<ApplicationResult<WorkflowSchedule?>> GetAsync(
        WorkflowScheduleId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all workflow schedules.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<WorkflowSchedule>>> ListAsync(
        CancellationToken cancellationToken);
}
