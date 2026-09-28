using FlowForge.Abstractions.Sharing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;
using FlowForge.Infrastructure.Sharing;

namespace FlowForge.Infrastructure.Tests.Sharing;

public sealed class InMemoryWorkflowSharingStoreTests
{
    [Fact]
    public async Task SaveAndGet_PreservesImmutableSnapshotAndOwnership()
    {
        IWorkflowSharingStore store = new InMemoryWorkflowSharingStore();
        var mutableTargets = new List<TenantId> { new(Guid.NewGuid()) };
        var sharing = CreateSharing(mutableTargets);

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

        Assert.NotNull(first);
        Assert.Single(first.SharedTenantIds);
        Assert.Equal(sharing.OwnerTenantId, first.OwnerTenantId);
        Assert.Equal(sharing.Visibility, first.Visibility);
        Assert.NotSame(first, second);
        Assert.NotSame(first.SharedTenantIds, second!.SharedTenantIds);
    }

    [Fact]
    public async Task SaveAsync_DuplicateDefinitionVersion_IsRejected()
    {
        var store = new InMemoryWorkflowSharingStore();
        var sharing = CreateSharing([new TenantId(Guid.NewGuid())]);
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

    [Fact]
    public async Task SaveAsync_DuplicateSharingIdentity_IsRejected()
    {
        var store = new InMemoryWorkflowSharingStore();
        var sharing = CreateSharing([new TenantId(Guid.NewGuid())]);
        await store.SaveAsync(sharing, CancellationToken.None);
        var anotherDefinition = CreateSharing([new TenantId(Guid.NewGuid())]) with
        {
            Id = sharing.Id
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(anotherDefinition, CancellationToken.None));

        var stored = await store.GetAsync(
            anotherDefinition.WorkflowDefinitionId,
            anotherDefinition.DefinitionVersion,
            CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task GetAsync_MissingSharing_ReturnsNull()
    {
        var store = new InMemoryWorkflowSharingStore();

        var sharing = await store.GetAsync(
            new WorkflowDefinitionId(Guid.NewGuid()),
            "1.0",
            CancellationToken.None);

        Assert.Null(sharing);
    }

    private static WorkflowSharing CreateSharing(
        IReadOnlyList<TenantId> targets) =>
        new()
        {
            Id = new WorkflowSharingId(Guid.NewGuid()),
            WorkflowDefinitionId = new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = "1.0",
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Visibility = WorkflowVisibility.Shared,
            SharedTenantIds = targets,
            CreatedAt = DateTime.UtcNow
        };
}
