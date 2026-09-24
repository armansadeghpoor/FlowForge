using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Abstractions.Schedules;

/// <summary>
/// Defines provider-independent storage operations for workflow schedules.
/// </summary>
public interface IWorkflowScheduleStore
{
    /// <summary>
    /// Saves an immutable workflow schedule.
    /// </summary>
    /// <param name="schedule">The schedule to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentException">Required schedule metadata is invalid.</exception>
    /// <exception cref="InvalidOperationException">The schedule identifier already exists.</exception>
    Task SaveAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a schedule by its identifier.
    /// </summary>
    /// <param name="id">The schedule identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The schedule, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowSchedule?> GetAsync(
        WorkflowScheduleId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored workflow schedules.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored workflow schedules.</returns>
    Task<IReadOnlyList<WorkflowSchedule>> ListAsync(
        CancellationToken cancellationToken);
}
