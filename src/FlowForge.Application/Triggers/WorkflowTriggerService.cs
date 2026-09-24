using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Triggers;

/// <summary>
/// Coordinates workflow trigger validation and persistence use cases.
/// </summary>
public sealed class WorkflowTriggerService : IWorkflowTriggerService
{
    private readonly IWorkflowTriggerStore _store;

    /// <summary>
    /// Initializes a workflow trigger application service.
    /// </summary>
    public WorkflowTriggerService(IWorkflowTriggerStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowTrigger>> CreateAsync(
        WorkflowTrigger trigger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var validationErrors = Validate(trigger);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<WorkflowTrigger>.Failure(validationErrors);
        }

        try
        {
            await _store.SaveAsync(trigger, cancellationToken);
            return ApplicationResult<WorkflowTrigger>.Success(trigger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ApplicationResult<WorkflowTrigger>.Failure(
                new ApplicationError(
                    "TriggerAlreadyExists",
                    "The workflow trigger already exists."));
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowTrigger>.Failure(
                PersistenceError("TriggerPersistenceFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowTrigger?>> GetAsync(
        WorkflowTriggerId id,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty)
        {
            return ApplicationResult<WorkflowTrigger?>.Failure(
                new ApplicationError(
                    "TriggerIdRequired",
                    "A workflow trigger identifier is required."));
        }

        try
        {
            var trigger = await _store.GetAsync(id, cancellationToken);
            return ApplicationResult<WorkflowTrigger?>.Success(trigger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowTrigger?>.Failure(
                PersistenceError("TriggerReadFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<WorkflowTrigger>>> ListAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var triggers = await _store.ListAsync(cancellationToken);
            return ApplicationResult<IReadOnlyList<WorkflowTrigger>>.Success(triggers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<WorkflowTrigger>>.Failure(
                PersistenceError("TriggerReadFailed"));
        }
    }

    private static IReadOnlyList<ApplicationError> Validate(WorkflowTrigger trigger)
    {
        var errors = new List<ApplicationError>();

        if (trigger.Id.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "TriggerIdRequired",
                "A workflow trigger identifier is required."));
        }

        if (trigger.WorkflowDefinitionId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "WorkflowDefinitionIdRequired",
                "A workflow definition identifier is required."));
        }

        if (string.IsNullOrWhiteSpace(trigger.DefinitionVersion))
        {
            errors.Add(new ApplicationError(
                "DefinitionVersionRequired",
                "A workflow definition version is required."));
        }

        if (!Enum.IsDefined(trigger.Type))
        {
            errors.Add(new ApplicationError(
                "TriggerTypeInvalid",
                "The workflow trigger type is not defined."));
        }

        if (trigger.Configuration is null)
        {
            errors.Add(new ApplicationError(
                "TriggerConfigurationRequired",
                "Workflow trigger configuration is required."));
        }

        return Array.AsReadOnly(errors.ToArray());
    }

    private static ApplicationError PersistenceError(string code) =>
        new(code, "The workflow trigger store operation failed.");
}
