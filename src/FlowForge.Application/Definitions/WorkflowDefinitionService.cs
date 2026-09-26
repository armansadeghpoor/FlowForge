using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Validation;
using FlowForge.Application.Auditing;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Definitions;

/// <summary>
/// Coordinates workflow definition validation and persistence use cases.
/// </summary>
public sealed class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDefinitionValidator _validator;
    private readonly IWorkflowDefinitionStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly ApplicationAuditRecorder _auditRecorder;

    /// <summary>
    /// Initializes a workflow definition application service.
    /// </summary>
    public WorkflowDefinitionService(
        IWorkflowDefinitionValidator validator,
        IWorkflowDefinitionStore store,
        IAuthorizationService authorizationService,
        IAuditStore auditStore,
        IAuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(auditStore);
        ArgumentNullException.ThrowIfNull(auditContext);
        _validator = validator;
        _store = store;
        _authorizationService = authorizationService;
        _auditRecorder = new ApplicationAuditRecorder(auditStore, auditContext);
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowDefinition>> CreateAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!await IsAuthorizedAsync(
                Permissions.WorkflowDefinitionsWrite,
                definition.OwnerTenantId,
                cancellationToken))
        {
            await AuditDefinitionAsync(
                ApplicationAuditActions.WorkflowDefinitionCreate,
                definition,
                AuditOutcome.Denied,
                cancellationToken);
            return PermissionDenied<WorkflowDefinition>();
        }

        var validation = _validator.Validate(definition);
        if (!validation.IsValid)
        {
            await AuditDefinitionAsync(
                ApplicationAuditActions.WorkflowDefinitionCreate,
                definition,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowDefinition>.Failure(
                validation.Errors.Select(error =>
                    new ApplicationError(error.Code, error.Message)));
        }

        try
        {
            await _store.SaveAsync(definition, cancellationToken);
            await AuditDefinitionAsync(
                ApplicationAuditActions.WorkflowDefinitionCreate,
                definition,
                AuditOutcome.Succeeded,
                cancellationToken);
            return ApplicationResult<WorkflowDefinition>.Success(definition);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            await AuditDefinitionAsync(
                ApplicationAuditActions.WorkflowDefinitionCreate,
                definition,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowDefinition>.Failure(
                new ApplicationError(
                    "DefinitionAlreadyExists",
                    "The workflow definition version already exists."));
        }
        catch (Exception)
        {
            await AuditDefinitionAsync(
                ApplicationAuditActions.WorkflowDefinitionCreate,
                definition,
                AuditOutcome.Failed,
                cancellationToken);
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
            await AuditDefinitionReferenceAsync(
                id,
                version,
                resourceTenantId: null,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowDefinition?>.Failure(
                new ApplicationError(
                    "DefinitionReferenceInvalid",
                    "A workflow definition identifier and version are required."));
        }

        if (!await IsAuthorizedAsync(
                Permissions.WorkflowDefinitionsRead,
                ownerTenantId: null,
                cancellationToken))
        {
            await AuditDefinitionReferenceAsync(
                id,
                version,
                resourceTenantId: null,
                AuditOutcome.Denied,
                cancellationToken);
            return PermissionDenied<WorkflowDefinition?>();
        }

        try
        {
            var definition = await _store.GetAsync(id, version, cancellationToken);
            if (definition is not null &&
                !await IsAuthorizedAsync(
                    Permissions.WorkflowDefinitionsRead,
                    definition.OwnerTenantId,
                    cancellationToken))
            {
                await AuditDefinitionAsync(
                    ApplicationAuditActions.WorkflowDefinitionRead,
                    definition,
                    AuditOutcome.Denied,
                    cancellationToken);
                return PermissionDenied<WorkflowDefinition?>();
            }

            await AuditDefinitionReferenceAsync(
                id,
                version,
                definition?.OwnerTenantId,
                definition is null
                    ? AuditOutcome.Failed
                    : AuditOutcome.Succeeded,
                cancellationToken);

            return ApplicationResult<WorkflowDefinition?>.Success(definition);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await AuditDefinitionReferenceAsync(
                id,
                version,
                resourceTenantId: null,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowDefinition?>.Failure(
                PersistenceError("DefinitionReadFailed"));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<WorkflowDefinition>>> ListAsync(
        CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(
                Permissions.WorkflowDefinitionsRead,
                ownerTenantId: null,
                cancellationToken))
        {
            return PermissionDenied<IReadOnlyList<WorkflowDefinition>>();
        }

        try
        {
            var definitions = await _store.ListAsync(cancellationToken);
            foreach (var definition in definitions)
            {
                if (!await IsAuthorizedAsync(
                        Permissions.WorkflowDefinitionsRead,
                        definition.OwnerTenantId,
                        cancellationToken))
                {
                    return PermissionDenied<IReadOnlyList<WorkflowDefinition>>();
                }
            }

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

    private async Task<bool> IsAuthorizedAsync(
        string permission,
        TenantId? ownerTenantId,
        CancellationToken cancellationToken)
    {
        var decision = await _authorizationService.AuthorizeAsync(
            new AuthorizationRequest
            {
                Permission = permission,
                OwnerTenantId = ownerTenantId
            },
            cancellationToken);

        return decision.IsAllowed;
    }

    private static ApplicationResult<T> PermissionDenied<T>() =>
        ApplicationResult<T>.Failure(
            new ApplicationError(
                "PermissionDenied",
                "The current user does not have permission."));

    private Task AuditDefinitionAsync(
        string action,
        WorkflowDefinition definition,
        AuditOutcome outcome,
        CancellationToken cancellationToken) =>
        _auditRecorder.TryRecordAsync(
            action,
            ApplicationAuditResourceTypes.WorkflowDefinition,
            DefinitionResourceIdentifier(definition.Id, definition.Version),
            outcome,
            definition.OwnerTenantId,
            cancellationToken);

    private Task AuditDefinitionReferenceAsync(
        WorkflowDefinitionId id,
        string? version,
        TenantId? resourceTenantId,
        AuditOutcome outcome,
        CancellationToken cancellationToken) =>
        _auditRecorder.TryRecordAsync(
            ApplicationAuditActions.WorkflowDefinitionRead,
            ApplicationAuditResourceTypes.WorkflowDefinition,
            DefinitionResourceIdentifier(id, version),
            outcome,
            resourceTenantId,
            cancellationToken);

    private static string DefinitionResourceIdentifier(
        WorkflowDefinitionId id,
        string? version) =>
        $"{id.Value:D}:{version ?? string.Empty}";
}
