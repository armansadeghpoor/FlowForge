using System.Collections.Concurrent;
using FlowForge.Abstractions.Schedules;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Infrastructure.Schedules;

/// <summary>
/// Stores immutable workflow schedule snapshots in memory.
/// </summary>
public sealed class InMemoryWorkflowScheduleStore : IWorkflowScheduleStore
{
    private readonly ConcurrentDictionary<WorkflowScheduleId, WorkflowSchedule> _schedules = new();

    /// <inheritdoc />
    public Task SaveAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Validate(schedule);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_schedules.TryAdd(schedule.Id, Snapshot(schedule)))
        {
            throw new InvalidOperationException(
                $"Workflow schedule '{schedule.Id.Value}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowSchedule?> GetAsync(
        WorkflowScheduleId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var schedule = _schedules.TryGetValue(id, out var stored)
            ? Snapshot(stored)
            : null;

        return Task.FromResult(schedule);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowSchedule>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowSchedule> schedules = Array.AsReadOnly(
            _schedules.Values
                .OrderBy(schedule => schedule.CreatedAt)
                .ThenBy(schedule => schedule.Id.Value)
                .Select(Snapshot)
                .ToArray());

        return Task.FromResult(schedules);
    }

    private static void Validate(WorkflowSchedule schedule)
    {
        if (schedule.Id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow schedule identifier is required.",
                nameof(schedule));
        }

        if (schedule.WorkflowTriggerId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow trigger reference is required.",
                nameof(schedule));
        }

        if (!Enum.IsDefined(schedule.Type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(schedule),
                schedule.Type,
                "The workflow schedule type is not defined.");
        }

        if (string.IsNullOrWhiteSpace(schedule.Expression))
        {
            throw new ArgumentException(
                "A workflow schedule expression is required.",
                nameof(schedule));
        }

        if (string.IsNullOrWhiteSpace(schedule.TimeZone))
        {
            throw new ArgumentException(
                "A workflow schedule time zone is required.",
                nameof(schedule));
        }

        if (schedule.CreatedAt == default)
        {
            throw new ArgumentException(
                "A workflow schedule creation timestamp is required.",
                nameof(schedule));
        }
    }

    private static WorkflowSchedule Snapshot(WorkflowSchedule schedule) =>
        schedule with { };
}
