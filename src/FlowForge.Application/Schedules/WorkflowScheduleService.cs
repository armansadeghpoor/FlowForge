using FlowForge.Abstractions.Schedules;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Application.Schedules;

/// <summary>
/// Coordinates workflow schedule validation and persistence use cases.
/// </summary>
public sealed class WorkflowScheduleService : IWorkflowScheduleService
{
    private readonly IWorkflowScheduleStore _store;

    /// <summary>
    /// Initializes a workflow schedule application service.
    /// </summary>
    public WorkflowScheduleService(IWorkflowScheduleStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowSchedule>> CreateAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var validationErrors = Validate(schedule);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<WorkflowSchedule>.Failure(validationErrors);
        }

        try
        {
            await _store.SaveAsync(schedule, cancellationToken);
            return ApplicationResult<WorkflowSchedule>.Success(schedule);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ApplicationResult<WorkflowSchedule>.Failure(
                new ApplicationError(
                    "ScheduleAlreadyExists",
                    "The workflow schedule already exists."));
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowSchedule>.Failure(
                PersistenceError("SchedulePersistenceFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowSchedule?>> GetAsync(
        WorkflowScheduleId id,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty)
        {
            return ApplicationResult<WorkflowSchedule?>.Failure(
                new ApplicationError(
                    "ScheduleIdRequired",
                    "A workflow schedule identifier is required."));
        }

        try
        {
            var schedule = await _store.GetAsync(id, cancellationToken);
            return ApplicationResult<WorkflowSchedule?>.Success(schedule);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowSchedule?>.Failure(
                PersistenceError("ScheduleReadFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<WorkflowSchedule>>> ListAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var schedules = await _store.ListAsync(cancellationToken);
            return ApplicationResult<IReadOnlyList<WorkflowSchedule>>.Success(schedules);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<WorkflowSchedule>>.Failure(
                PersistenceError("ScheduleReadFailed"));
        }
    }

    private static IReadOnlyList<ApplicationError> Validate(WorkflowSchedule schedule)
    {
        var errors = new List<ApplicationError>();

        if (schedule.Id.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "ScheduleIdRequired",
                "A workflow schedule identifier is required."));
        }

        if (schedule.WorkflowTriggerId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "WorkflowTriggerIdRequired",
                "A workflow trigger reference is required."));
        }

        if (!Enum.IsDefined(schedule.Type))
        {
            errors.Add(new ApplicationError(
                "ScheduleTypeInvalid",
                "The workflow schedule type is not defined."));
        }

        if (string.IsNullOrWhiteSpace(schedule.Expression))
        {
            errors.Add(new ApplicationError(
                "ScheduleExpressionRequired",
                "A workflow schedule expression is required."));
        }

        if (string.IsNullOrWhiteSpace(schedule.TimeZone))
        {
            errors.Add(new ApplicationError(
                "ScheduleTimeZoneRequired",
                "A workflow schedule time zone is required."));
        }

        if (schedule.CreatedAt == default)
        {
            errors.Add(new ApplicationError(
                "ScheduleCreatedAtRequired",
                "A workflow schedule creation timestamp is required."));
        }

        return Array.AsReadOnly(errors.ToArray());
    }

    private static ApplicationError PersistenceError(string code) =>
        new(code, "The workflow schedule store operation failed.");
}
