using FlowForge.Abstractions.Schedules;
using FlowForge.Abstractions.Scheduling;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Engine.Scheduling;

/// <summary>
/// Evaluates and dispatches due workflow schedule occurrences.
/// </summary>
public sealed class WorkflowScheduler : IWorkflowScheduler
{
    private readonly IWorkflowScheduleStore _scheduleStore;
    private readonly IScheduleEvaluator _scheduleEvaluator;
    private readonly IScheduleExecutionTracker _executionTracker;
    private readonly IWorkflowTriggerExecutor _triggerExecutor;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a workflow scheduler.
    /// </summary>
    public WorkflowScheduler(
        IWorkflowScheduleStore scheduleStore,
        IScheduleEvaluator scheduleEvaluator,
        IScheduleExecutionTracker executionTracker,
        IWorkflowTriggerExecutor triggerExecutor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scheduleStore);
        ArgumentNullException.ThrowIfNull(scheduleEvaluator);
        ArgumentNullException.ThrowIfNull(executionTracker);
        ArgumentNullException.ThrowIfNull(triggerExecutor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _scheduleStore = scheduleStore;
        _scheduleEvaluator = scheduleEvaluator;
        _executionTracker = executionTracker;
        _triggerExecutor = triggerExecutor;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleExecutionResult>> RunDueSchedulesAsync(
        CancellationToken cancellationToken)
    {
        var schedules = await _scheduleStore.ListAsync(cancellationToken);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<ScheduleExecutionResult>();

        foreach (var schedule in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!schedule.Enabled)
            {
                continue;
            }

            var occurrenceUtc = await GetNextOccurrenceAsync(
                schedule,
                cancellationToken);
            if (occurrenceUtc is null || occurrenceUtc > nowUtc)
            {
                continue;
            }

            if (!await _executionTracker.TryTrackAsync(
                    schedule.Id,
                    occurrenceUtc.Value,
                    cancellationToken))
            {
                continue;
            }

            results.Add(await ExecuteOccurrenceAsync(
                schedule,
                occurrenceUtc.Value,
                nowUtc,
                cancellationToken));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    private async Task<DateTime?> GetNextOccurrenceAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken)
    {
        var lastOccurrence = await _executionTracker.GetLastTrackedOccurrenceAsync(
            schedule.Id,
            cancellationToken);
        var fromUtc = lastOccurrence ?? BeforeCreation(schedule.CreatedAt);
        return _scheduleEvaluator.GetNextOccurrence(schedule, fromUtc);
    }

    private async Task<ScheduleExecutionResult> ExecuteOccurrenceAsync(
        WorkflowSchedule schedule,
        DateTime occurrenceUtc,
        DateTime requestedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var workflowExecutionId = await _triggerExecutor.ExecuteAsync(
                new WorkflowTriggerExecutionContext
                {
                    TriggerId = schedule.WorkflowTriggerId,
                    TriggerType = TriggerType.Timer,
                    CorrelationId = $"schedule:{schedule.Id.Value:N}:{occurrenceUtc.Ticks}",
                    RequestedAt = requestedAt
                },
                cancellationToken);

            return CreateResult(schedule, occurrenceUtc, true, workflowExecutionId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateResult(schedule, occurrenceUtc, false, null);
        }
    }

    private static ScheduleExecutionResult CreateResult(
        WorkflowSchedule schedule,
        DateTime occurrenceUtc,
        bool success,
        WorkflowExecutionId? workflowExecutionId) =>
        new()
        {
            ScheduleId = schedule.Id,
            TriggerId = schedule.WorkflowTriggerId,
            OccurrenceUtc = occurrenceUtc,
            Success = success,
            WorkflowExecutionId = workflowExecutionId
        };

    private static DateTime BeforeCreation(DateTime createdAt)
    {
        var createdAtUtc = createdAt.Kind == DateTimeKind.Utc
            ? createdAt
            : createdAt.ToUniversalTime();
        return createdAtUtc == DateTime.MinValue
            ? createdAtUtc
            : createdAtUtc.AddTicks(-1);
    }
}
