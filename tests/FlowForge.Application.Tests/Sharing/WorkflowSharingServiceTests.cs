using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Sharing;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Abstractions.Validation;
using FlowForge.Application.Definitions;
using FlowForge.Application.Security;
using FlowForge.Application.Sharing;
using FlowForge.Application.Tests.Auditing;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Application.Tests.Sharing;

public sealed class WorkflowSharingServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidSharing_PreservesOwnerAndPersistsMetadata()
    {
        var definition = CreateDefinition();
        var sharing = CreateSharing(
            definition,
            new TenantId(Guid.NewGuid()));
        var sharingStore = new SharingStoreStub();
        var auditStore = new RecordingAuditStore();
        var service = CreateService(
            definition,
            sharingStore,
            definition.OwnerTenantId,
            Permissions.WorkflowDefinitionsShare,
            auditStore);

        var result = await service.CreateAsync(sharing, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(sharing, sharingStore.SavedSharing);
        Assert.Equal(definition.OwnerTenantId, result.Value!.OwnerTenantId);
        Assert.Equal(definition.OwnerTenantId, sharingStore.SavedSharing!.OwnerTenantId);
        var audit = Assert.Single(auditStore.Entries);
        Assert.Equal("WorkflowSharing.Create", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task CreateAsync_OwnerMismatch_IsRejectedWithoutChangingDefinitionOwner()
    {
        var definition = CreateDefinition();
        var originalOwner = definition.OwnerTenantId;
        var claimedOwner = new TenantId(Guid.NewGuid());
        var sharing = CreateSharing(
            definition,
            new TenantId(Guid.NewGuid())) with
        {
            OwnerTenantId = claimedOwner
        };
        var sharingStore = new SharingStoreStub();
        var service = CreateService(
            definition,
            sharingStore,
            claimedOwner,
            Permissions.WorkflowDefinitionsShare);

        var result = await service.CreateAsync(sharing, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SharingOwnerMismatch", Assert.Single(result.Errors).Code);
        Assert.Null(sharingStore.SavedSharing);
        Assert.Equal(originalOwner, definition.OwnerTenantId);
    }

    [Fact]
    public async Task ResolveVisibilityAsync_ExplicitSharedTenant_IsVisible()
    {
        var definition = CreateDefinition();
        var requestTenantId = new TenantId(Guid.NewGuid());
        var sharingStore = new SharingStoreStub
        {
            SharingToReturn = CreateSharing(definition, requestTenantId)
        };
        var service = CreateService(
            definition,
            sharingStore,
            requestTenantId,
            Permissions.WorkflowDefinitionsRead);

        var result = await service.ResolveVisibilityAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ResolveVisibilityAsync_UnlistedTenant_IsNotVisible()
    {
        var definition = CreateDefinition();
        var requestTenantId = new TenantId(Guid.NewGuid());
        var sharingStore = new SharingStoreStub
        {
            SharingToReturn = CreateSharing(
                definition,
                new TenantId(Guid.NewGuid()))
        };
        var service = CreateService(
            definition,
            sharingStore,
            requestTenantId,
            Permissions.WorkflowDefinitionsRead);

        var result = await service.ResolveVisibilityAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task CreateAsync_MissingSharingPermission_IsDenied()
    {
        var definition = CreateDefinition();
        var sharingStore = new SharingStoreStub();
        var service = CreateService(
            definition,
            sharingStore,
            definition.OwnerTenantId,
            Permissions.WorkflowDefinitionsRead);

        var result = await service.CreateAsync(
            CreateSharing(definition, new TenantId(Guid.NewGuid())),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PermissionDenied", Assert.Single(result.Errors).Code);
        Assert.Null(sharingStore.SavedSharing);
    }

    [Theory]
    [InlineData(WorkflowVisibility.Private)]
    [InlineData(WorkflowVisibility.TenantVisible)]
    public async Task ResolveVisibilityAsync_NonSharedCrossTenantWorkflow_IsNotVisible(
        WorkflowVisibility visibility)
    {
        var definition = CreateDefinition();
        var requestTenantId = new TenantId(Guid.NewGuid());
        var sharingStore = new SharingStoreStub
        {
            SharingToReturn = CreateSharing(definition, requestTenantId) with
            {
                Visibility = visibility,
                SharedTenantIds = []
            }
        };
        var service = CreateService(
            definition,
            sharingStore,
            requestTenantId,
            Permissions.WorkflowDefinitionsRead);

        var result = await service.ResolveVisibilityAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task WorkflowDefinitionGetAsync_SharedTenantCanReadWithoutChangingOwner()
    {
        var definition = CreateDefinition();
        var requestTenantId = new TenantId(Guid.NewGuid());
        var sharingStore = new SharingStoreStub
        {
            SharingToReturn = CreateSharing(definition, requestTenantId)
        };
        var authorization = CreateAuthorization(
            requestTenantId,
            Permissions.WorkflowDefinitionsRead);
        var service = new WorkflowDefinitionService(
            new ValidDefinitionValidator(),
            new DefinitionStoreStub(definition),
            authorization,
            sharingStore,
            new RecordingAuditStore(),
            StaticAuditContext.Create(tenantId: requestTenantId));

        var result = await service.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(definition.OwnerTenantId, result.Value!.OwnerTenantId);
    }

    private static WorkflowSharingService CreateService(
        WorkflowDefinition definition,
        SharingStoreStub sharingStore,
        TenantId currentTenantId,
        string permission,
        RecordingAuditStore? auditStore = null) =>
        new(
            new DefinitionStoreStub(definition),
            sharingStore,
            CreateAuthorization(currentTenantId, permission),
            auditStore ?? new RecordingAuditStore(),
            StaticAuditContext.Create(tenantId: currentTenantId));

    private static PermissionAuthorizationService CreateAuthorization(
        TenantId tenantId,
        string permission) =>
        new(
            new UserContextStub(
                new SecurityContext(
                    "user-1",
                    true,
                    [permission],
                    tenantId.Value.ToString("D"))),
            new TenantContext(tenantId));

    private static WorkflowDefinition CreateDefinition() =>
        new()
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Name = "Shared workflow",
            Version = "1.0",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Nodes = [],
            Edges = []
        };

    private static WorkflowSharing CreateSharing(
        WorkflowDefinition definition,
        TenantId sharedTenantId) =>
        new()
        {
            Id = new WorkflowSharingId(Guid.NewGuid()),
            WorkflowDefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            OwnerTenantId = definition.OwnerTenantId,
            Visibility = WorkflowVisibility.Shared,
            SharedTenantIds = [sharedTenantId],
            CreatedAt = DateTime.UtcNow
        };

    private sealed class DefinitionStoreStub(WorkflowDefinition definition)
        : IWorkflowDefinitionStore
    {
        public Task SaveAsync(
            WorkflowDefinition value,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkflowDefinition?> GetAsync(
            WorkflowDefinitionId id,
            string version,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkflowDefinition?>(definition);

        public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowDefinition>>([definition]);
    }

    private sealed class SharingStoreStub : IWorkflowSharingStore
    {
        public WorkflowSharing? SharingToReturn { get; init; }

        public WorkflowSharing? SavedSharing { get; private set; }

        public Task SaveAsync(
            WorkflowSharing sharing,
            CancellationToken cancellationToken)
        {
            SavedSharing = sharing;
            return Task.CompletedTask;
        }

        public Task<WorkflowSharing?> GetAsync(
            WorkflowDefinitionId workflowDefinitionId,
            string definitionVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(SharingToReturn);
    }

    private sealed class UserContextStub(SecurityContext current) : IUserContext
    {
        public SecurityContext Current { get; } = current;
    }

    private sealed class ValidDefinitionValidator : IWorkflowDefinitionValidator
    {
        public WorkflowDefinitionValidationResult Validate(
            WorkflowDefinition definition) => new([]);
    }
}
