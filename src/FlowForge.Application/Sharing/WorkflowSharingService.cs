using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Sharing;
using FlowForge.Application.Auditing;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Application.Sharing;

/// <summary>
/// Coordinates immutable workflow sharing and visibility use cases.
/// </summary>
public sealed class WorkflowSharingService : IWorkflowSharingService
{
    private readonly IWorkflowDefinitionStore _definitionStore;
    private readonly IWorkflowSharingStore _sharingStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly ApplicationAuditRecorder _auditRecorder;

    /// <summary>Initializes the workflow sharing application service.</summary>
    public WorkflowSharingService(
        IWorkflowDefinitionStore definitionStore,
        IWorkflowSharingStore sharingStore,
        IAuthorizationService authorizationService,
        IAuditStore auditStore,
        IAuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(definitionStore);
        ArgumentNullException.ThrowIfNull(sharingStore);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(auditStore);
        ArgumentNullException.ThrowIfNull(auditContext);
        _definitionStore = definitionStore;
        _sharingStore = sharingStore;
        _authorizationService = authorizationService;
        _auditRecorder = new ApplicationAuditRecorder(auditStore, auditContext);
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowSharing>> CreateAsync(
        WorkflowSharing sharing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sharing);

        if (!await IsAuthorizedAsync(
                Permissions.WorkflowDefinitionsShare,
                sharing.OwnerTenantId,
                sharing.WorkflowDefinitionId,
                sharing.DefinitionVersion,
                sharing: null,
                cancellationToken))
        {
            await AuditAsync(sharing, AuditOutcome.Denied, cancellationToken);
            return PermissionDenied<WorkflowSharing>();
        }

        var validationErrors = Validate(sharing);
        if (validationErrors.Count > 0)
        {
            await AuditAsync(sharing, AuditOutcome.Failed, cancellationToken);
            return ApplicationResult<WorkflowSharing>.Failure(validationErrors);
        }

        try
        {
            var definition = await _definitionStore.GetAsync(
                sharing.WorkflowDefinitionId,
                sharing.DefinitionVersion,
                cancellationToken);
            if (definition is null)
            {
                await AuditAsync(sharing, AuditOutcome.Failed, cancellationToken);
                return ApplicationResult<WorkflowSharing>.Failure(
                    new ApplicationError(
                        "DefinitionNotFound",
                        "The workflow definition version was not found."));
            }

            if (definition.OwnerTenantId != sharing.OwnerTenantId)
            {
                await AuditAsync(sharing, AuditOutcome.Failed, cancellationToken);
                return ApplicationResult<WorkflowSharing>.Failure(
                    new ApplicationError(
                        "SharingOwnerMismatch",
                        "Sharing metadata must preserve the workflow owner."));
            }

            await _sharingStore.SaveAsync(sharing, cancellationToken);
            await AuditAsync(sharing, AuditOutcome.Succeeded, cancellationToken);
            return ApplicationResult<WorkflowSharing>.Success(sharing);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            await AuditAsync(sharing, AuditOutcome.Failed, cancellationToken);
            return ApplicationResult<WorkflowSharing>.Failure(
                new ApplicationError(
                    "SharingAlreadyExists",
                    "Sharing metadata already exists for the workflow definition version."));
        }
        catch (Exception)
        {
            await AuditAsync(sharing, AuditOutcome.Failed, cancellationToken);
            return ApplicationResult<WorkflowSharing>.Failure(
                new ApplicationError(
                    "SharingPersistenceFailed",
                    "The workflow sharing store operation failed."));
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowSharing?>> GetAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(
            workflowDefinitionId,
            definitionVersion,
            cancellationToken);
        if (!loaded.IsSuccess)
        {
            return ApplicationResult<WorkflowSharing?>.Failure(loaded.Errors);
        }

        var state = loaded.Value!;
        if (!await IsAuthorizedAsync(
                Permissions.WorkflowDefinitionsRead,
                state.OwnerTenantId,
                workflowDefinitionId,
                definitionVersion,
                state.Sharing,
                cancellationToken))
        {
            return PermissionDenied<WorkflowSharing?>();
        }

        return ApplicationResult<WorkflowSharing?>.Success(state.Sharing);
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<bool>> ResolveVisibilityAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(
            workflowDefinitionId,
            definitionVersion,
            cancellationToken);
        if (!loaded.IsSuccess)
        {
            return ApplicationResult<bool>.Failure(loaded.Errors);
        }

        var state = loaded.Value!;
        var allowed = await IsAuthorizedAsync(
            Permissions.WorkflowDefinitionsRead,
            state.OwnerTenantId,
            workflowDefinitionId,
            definitionVersion,
            state.Sharing,
            cancellationToken);

        return ApplicationResult<bool>.Success(allowed);
    }

    private async Task<ApplicationResult<SharingState>> LoadAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        if (workflowDefinitionId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(definitionVersion))
        {
            return ApplicationResult<SharingState>.Failure(
                new ApplicationError(
                    "DefinitionReferenceInvalid",
                    "A workflow definition identifier and version are required."));
        }

        try
        {
            var definition = await _definitionStore.GetAsync(
                workflowDefinitionId,
                definitionVersion,
                cancellationToken);
            if (definition is null)
            {
                return ApplicationResult<SharingState>.Failure(
                    new ApplicationError(
                        "DefinitionNotFound",
                        "The workflow definition version was not found."));
            }

            var sharing = await _sharingStore.GetAsync(
                workflowDefinitionId,
                definitionVersion,
                cancellationToken);
            return ApplicationResult<SharingState>.Success(
                new SharingState(definition.OwnerTenantId, sharing));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<SharingState>.Failure(
                new ApplicationError(
                    "SharingReadFailed",
                    "The workflow sharing store operation failed."));
        }
    }

    private Task<AuthorizationDecision> AuthorizeAsync(
        string permission,
        TenantId ownerTenantId,
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        WorkflowSharing? sharing,
        CancellationToken cancellationToken) =>
        _authorizationService.AuthorizeAsync(
            new AuthorizationRequest
            {
                Permission = permission,
                OwnerTenantId = ownerTenantId,
                WorkflowDefinitionId = workflowDefinitionId,
                DefinitionVersion = definitionVersion,
                Sharing = sharing
            },
            cancellationToken);

    private async Task<bool> IsAuthorizedAsync(
        string permission,
        TenantId ownerTenantId,
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        WorkflowSharing? sharing,
        CancellationToken cancellationToken) =>
        (await AuthorizeAsync(
            permission,
            ownerTenantId,
            workflowDefinitionId,
            definitionVersion,
            sharing,
            cancellationToken)).IsAllowed;

    private static IReadOnlyList<ApplicationError> Validate(WorkflowSharing sharing)
    {
        var errors = new List<ApplicationError>();
        if (sharing.Id.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "SharingIdRequired",
                "A sharing identifier is required."));
        }

        if (sharing.WorkflowDefinitionId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "SharingDefinitionIdRequired",
                "A workflow definition identifier is required."));
        }

        if (string.IsNullOrWhiteSpace(sharing.DefinitionVersion))
        {
            errors.Add(new ApplicationError(
                "SharingDefinitionVersionRequired",
                "A workflow definition version is required."));
        }

        if (sharing.OwnerTenantId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "SharingOwnerRequired",
                "A workflow owner tenant is required."));
        }

        if (!Enum.IsDefined(sharing.Visibility))
        {
            errors.Add(new ApplicationError(
                "SharingVisibilityInvalid",
                "The workflow visibility is invalid."));
        }

        if (sharing.SharedTenantIds is null)
        {
            errors.Add(new ApplicationError(
                "SharedTenantsRequired",
                "A shared tenant collection is required."));
            return errors;
        }

        if (sharing.Visibility == WorkflowVisibility.Shared &&
            sharing.SharedTenantIds.Count == 0)
        {
            errors.Add(new ApplicationError(
                "SharedTenantsRequired",
                "Shared workflows require at least one target tenant."));
        }

        if (sharing.Visibility != WorkflowVisibility.Shared &&
            sharing.SharedTenantIds.Count > 0)
        {
            errors.Add(new ApplicationError(
                "SharedTenantsNotAllowed",
                "Only shared workflows may specify target tenants."));
        }

        if (sharing.SharedTenantIds.Contains(sharing.OwnerTenantId))
        {
            errors.Add(new ApplicationError(
                "SharingOwnerCannotBeTarget",
                "The workflow owner cannot be a sharing target."));
        }

        if (sharing.SharedTenantIds.Any(tenantId => tenantId.Value == Guid.Empty))
        {
            errors.Add(new ApplicationError(
                "SharedTenantInvalid",
                "Shared tenant identifiers must not be empty."));
        }

        if (sharing.SharedTenantIds.Distinct().Count() !=
            sharing.SharedTenantIds.Count)
        {
            errors.Add(new ApplicationError(
                "SharedTenantsDuplicate",
                "Shared tenant identifiers must be unique."));
        }

        return errors;
    }

    private Task AuditAsync(
        WorkflowSharing sharing,
        AuditOutcome outcome,
        CancellationToken cancellationToken) =>
        _auditRecorder.TryRecordAsync(
            ApplicationAuditActions.WorkflowSharingCreate,
            ApplicationAuditResourceTypes.WorkflowDefinition,
            $"{sharing.WorkflowDefinitionId.Value:D}:{sharing.DefinitionVersion}",
            outcome,
            sharing.OwnerTenantId,
            cancellationToken);

    private static ApplicationResult<T> PermissionDenied<T>() =>
        ApplicationResult<T>.Failure(
            new ApplicationError(
                "PermissionDenied",
                "The current user does not have permission."));

    private sealed record SharingState(
        TenantId OwnerTenantId,
        WorkflowSharing? Sharing);
}
