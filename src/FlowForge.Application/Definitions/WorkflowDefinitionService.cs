using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Validation;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Definitions;

/// <summary>
/// Coordinates workflow definition validation and persistence use cases.
/// </summary>
public sealed class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDefinitionValidator _validator;
    private readonly IWorkflowDefinitionStore _store;

    /// <summary>
    /// Initializes a workflow definition application service.
    /// </summary>
    public WorkflowDefinitionService(
        IWorkflowDefinitionValidator validator,
        IWorkflowDefinitionStore store)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(store);
        _validator = validator;
        _store = store;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowDefinition>> CreateAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var validation = _validator.Validate(definition);
        if (!validation.IsValid)
        {
            return ApplicationResult<WorkflowDefinition>.Failure(
                validation.Errors.Select(error =>
                    new ApplicationError(error.Code, error.Message)));
        }

        try
        {
            await _store.SaveAsync(definition, cancellationToken);
            return ApplicationResult<WorkflowDefinition>.Success(definition);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return ApplicationResult<WorkflowDefinition>.Failure(
                new ApplicationError(
                    "DefinitionAlreadyExists",
                    "The workflow definition version already exists."));
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowDefinition>.Failure(
                PersistenceError("DefinitionPersistenceFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowDefinition?>> GetAsync(
        WorkflowDefinitionId id,
        string version,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty || string.IsNullOrWhiteSpace(version))
        {
            return ApplicationResult<WorkflowDefinition?>.Failure(
                new ApplicationError(
                    "DefinitionReferenceInvalid",
                    "A workflow definition identifier and version are required."));
        }

        try
        {
            var definition = await _store.GetAsync(id, version, cancellationToken);
            return ApplicationResult<WorkflowDefinition?>.Success(definition);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowDefinition?>.Failure(
                PersistenceError("DefinitionReadFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<WorkflowDefinition>>> ListAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var definitions = await _store.ListAsync(cancellationToken);
            return ApplicationResult<IReadOnlyList<WorkflowDefinition>>.Success(definitions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<WorkflowDefinition>>.Failure(
                PersistenceError("DefinitionReadFailed"));
        }
    }

    private static ApplicationError PersistenceError(string code) =>
        new(code, "The workflow definition store operation failed.");
}
