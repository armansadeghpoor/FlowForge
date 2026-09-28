using FlowForge.Abstractions.Sharing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Infrastructure.Tests.Sharing.Conformance;

public abstract class WorkflowSharingStoreConformanceTests
{
    protected abstract IWorkflowSharingStore CreateStore();

    [SkippableFact]
    public async Task SaveAndGet_PreservesAllSharingFields()
    {
        var store = CreateStore();
        var sharing = CreateSharing();

        await store.SaveAsync(sharing, CancellationToken.None);
        var stored = await store.GetAsync(
            sharing.WorkflowDefinitionId,
            sharing.DefinitionVersion,
            CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(sharing.Id, stored.Id);
        Assert.Equal(sharing.WorkflowDefinitionId, stored.WorkflowDefinitionId);
        Assert.Equal(sharing.DefinitionVersion, stored.DefinitionVersion);
        Assert.Equal(sharing.OwnerTenantId, stored.OwnerTenantId);
        Assert.Equal(sharing.Visibility, stored.Visibility);
        Assert.Equal(sharing.SharedTenantIds, stored.SharedTenantIds);
        Assert.Equal(sharing.CreatedAt, stored.CreatedAt);
    }

    [SkippableFact]
    public async Task GetAsync_UsesExactDefinitionVersion()
    {
        var store = CreateStore();
        var definitionId = new WorkflowDefinitionId(Guid.NewGuid());
        var first = CreateSharing() with
        {
            WorkflowDefinitionId = definitionId,
            DefinitionVersion = "1.0"
        };
        var second = CreateSharing() with
        {
            WorkflowDefinitionId = definitionId,
            DefinitionVersion = "2.0"
        };
        await store.SaveAsync(first, CancellationToken.None);
        await store.SaveAsync(second, CancellationToken.None);

        var firstStored = await store.GetAsync(
            definitionId,
            "1.0",
            CancellationToken.None);
        var secondStored = await store.GetAsync(
            definitionId,
            "2.0",
            CancellationToken.None);

        Assert.Equal(first.Id, firstStored!.Id);
        Assert.Equal(second.Id, secondStored!.Id);
    }

    [SkippableFact]
    public async Task SaveAndGet_PreservesOwnerAndSharedTenants()
    {
        var store = CreateStore();
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var sharedTenantIds = new[]
        {
            new TenantId(Guid.NewGuid()),
            new TenantId(Guid.NewGuid())
        };
        var sharing = CreateSharing() with
        {
            OwnerTenantId = ownerTenantId,
            SharedTenantIds = sharedTenantIds
        };

        await store.SaveAsync(sharing, CancellationToken.None);
        var stored = await store.GetAsync(
            sharing.WorkflowDefinitionId,
            sharing.DefinitionVersion,
            CancellationToken.None);

        Assert.Equal(ownerTenantId, stored!.OwnerTenantId);
        Assert.Equal(sharedTenantIds, stored.SharedTenantIds);
    }

    [SkippableFact]
    public async Task SaveAndGet_PreservesEveryVisibilityValue()
    {
        var store = CreateStore();
        var sharing = Enum.GetValues<WorkflowVisibility>()
            .Select(visibility => CreateSharing() with { Visibility = visibility })
            .ToArray();
        foreach (var item in sharing)
        {
            await store.SaveAsync(item, CancellationToken.None);
        }

        foreach (var item in sharing)
        {
            var stored = await store.GetAsync(
                item.WorkflowDefinitionId,
                item.DefinitionVersion,
                CancellationToken.None);
            Assert.Equal(item.Visibility, stored!.Visibility);
        }
    }

    [SkippableFact]
    public async Task SaveAndGet_UsesImmutableSnapshots()
    {
        var store = CreateStore();
        var mutableTargets = new List<TenantId> { new(Guid.NewGuid()) };
        var sharing = CreateSharing() with { SharedTenantIds = mutableTargets };

        await store.SaveAsync(sharing, CancellationToken.None);
        mutableTargets.Add(new TenantId(Guid.NewGuid()));
        var first = await store.GetAsync(
            sharing.WorkflowDefinitionId,
            sharing.DefinitionVersion,
            CancellationToken.None);
        var second = await store.GetAsync(
            sharing.WorkflowDefinitionId,
            sharing.DefinitionVersion,
            CancellationToken.None);

        Assert.Single(first!.SharedTenantIds);
        Assert.NotSame(first, second);
        Assert.NotSame(first.SharedTenantIds, second!.SharedTenantIds);
    }

    [SkippableFact]
    public async Task SaveAsync_DuplicateDefinitionVersion_IsRejected()
    {
        var store = CreateStore();
        var sharing = CreateSharing();
        await store.SaveAsync(sharing, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(
                sharing with { Id = new WorkflowSharingId(Guid.NewGuid()) },
                CancellationToken.None));

        var stored = await store.GetAsync(
            sharing.WorkflowDefinitionId,
            sharing.DefinitionVersion,
            CancellationToken.None);
        Assert.Equal(sharing.Id, stored!.Id);
    }

    [SkippableFact]
    public async Task SaveAsync_DuplicateSharingIdentity_IsRejected()
    {
        var store = CreateStore();
        var sharing = CreateSharing();
        await store.SaveAsync(sharing, CancellationToken.None);
        var anotherDefinition = CreateSharing() with { Id = sharing.Id };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(anotherDefinition, CancellationToken.None));

        var stored = await store.GetAsync(
            anotherDefinition.WorkflowDefinitionId,
            anotherDefinition.DefinitionVersion,
            CancellationToken.None);
        Assert.Null(stored);
    }

    [SkippableFact]
    public async Task GetAsync_MissingSharing_ReturnsNull()
    {
        var sharing = await CreateStore().GetAsync(
            new WorkflowDefinitionId(Guid.NewGuid()),
            "1.0",
            CancellationToken.None);

        Assert.Null(sharing);
    }

    private static WorkflowSharing CreateSharing() =>
        new()
        {
            Id = new WorkflowSharingId(Guid.NewGuid()),
            WorkflowDefinitionId = new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = "1.0",
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Visibility = WorkflowVisibility.Shared,
            SharedTenantIds = Array.AsReadOnly(
                new[] { new TenantId(Guid.NewGuid()) }),
            CreatedAt = new DateTime(
                2026,
                9,
                28,
                10,
                0,
                0,
                DateTimeKind.Utc)
        };
}
