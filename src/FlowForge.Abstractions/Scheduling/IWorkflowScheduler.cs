namespace FlowForge.Abstractions.Scheduling;

/// <summary>
/// Defines explicit workflow schedule evaluation and dispatch.
/// </summary>
public interface IWorkflowScheduler
{
    /// <summary>
    /// Evaluates schedules and dispatches occurrences that are currently due.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The execution results for due, newly tracked occurrences.</returns>
    Task<IReadOnlyList<ScheduleExecutionResult>> RunDueSchedulesAsync(
        CancellationToken cancellationToken);
}
