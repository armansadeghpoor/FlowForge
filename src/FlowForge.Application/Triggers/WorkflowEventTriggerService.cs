using System.Text.Json;
using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Triggers;

/// <summary>
/// Coordinates workflow event trigger validation and persistence use cases.
/// </summary>
public sealed class WorkflowEventTriggerService : IWorkflowEventTriggerService
{
    private readonly IWorkflowEventTriggerStore _store;

    /// <summary>
    /// Initializes a workflow event trigger application service.
    /// </summary>
    public WorkflowEventTriggerService(IWorkflowEventTriggerStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowEventTrigger>> CreateAsync(
        WorkflowEventTrigger eventTrigger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventTrigger);

        var validationErrors = Validate(eventTrigger);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<WorkflowEventTrigger>.Failure(validationErrors);
        }

        try
        {
            await _store.SaveAsync(eventTrigger, cancellationToken);
            return ApplicationResult<WorkflowEventTrigger>.Success(eventTrigger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ApplicationResult<WorkflowEventTrigger>.Failure(
                new ApplicationError(
                    "EventTriggerAlreadyExists",
                    "The workflow event trigger already exists."));
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowEventTrigger>.Failure(
                PersistenceError("EventTriggerPersistenceFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowEventTrigger?>> GetAsync(
        WorkflowEventTriggerId id,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty)
        {
            return ApplicationResult<WorkflowEventTrigger?>.Failure(
                new ApplicationError(
                    "EventTriggerIdRequired",
                    "A workflow event trigger identifier is required."));
        }

        try
        {
            var eventTrigger = await _store.GetAsync(id, cancellationToken);
            return ApplicationResult<WorkflowEventTrigger?>.Success(eventTrigger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowEventTrigger?>.Failure(
                PersistenceError("EventTriggerReadFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<WorkflowEventTrigger>>> ListAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var eventTriggers = await _store.ListAsync(cancellationToken);
            return ApplicationResult<IReadOnlyList<WorkflowEventTrigger>>.Success(
                eventTriggers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<WorkflowEventTrigger>>.Failure(
                PersistenceError("EventTriggerReadFailed"));
        }
    }

    private static IReadOnlyList<ApplicationError> Validate(
        WorkflowEventTrigger eventTrigger)
    {
        var errors = new List<ApplicationError>();

        if (eventTrigger.Id.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "EventTriggerIdRequired",
                "A workflow event trigger identifier is required."));
        }

        if (eventTrigger.WorkflowTriggerId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "WorkflowTriggerIdRequired",
                "A workflow trigger reference is required."));
        }

        if (string.IsNullOrWhiteSpace(eventTrigger.EventType))
        {
            errors.Add(new ApplicationError(
                "EventTypeRequired",
                "A workflow event type is required."));
        }

        if (!IsValidFilter(eventTrigger.Filter))
        {
            errors.Add(new ApplicationError(
                "EventFilterInvalid",
                "Workflow event filter metadata must contain valid JSON values."));
        }

        if (eventTrigger.CreatedAt == default)
        {
            errors.Add(new ApplicationError(
                "EventTriggerCreatedAtRequired",
                "A workflow event trigger creation timestamp is required."));
        }

        return Array.AsReadOnly(errors.ToArray());
    }

    private static bool IsValidFilter(
        IReadOnlyDictionary<string, JsonElement>? filter)
    {
        if (filter is null)
        {
            return true;
        }

        try
        {
            return filter.Values.All(value => value.ValueKind != JsonValueKind.Undefined);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static ApplicationError PersistenceError(string code) =>
        new(code, "The workflow event trigger store operation failed.");
}
