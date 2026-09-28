using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Core.Tests.Sharing;

public sealed class WorkflowSharingTests
{
    [Fact]
    public void WorkflowSharing_IsImmutableAndPreservesOwner()
    {
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var sharing = new WorkflowSharing
        {
            Id = new WorkflowSharingId(Guid.NewGuid()),
            WorkflowDefinitionId = new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = "1.0",
            OwnerTenantId = ownerTenantId,
            Visibility = WorkflowVisibility.Private,
            SharedTenantIds = [],
            CreatedAt = DateTime.UtcNow
        };

        var sharedCopy = sharing with
        {
            Visibility = WorkflowVisibility.Shared,
            SharedTenantIds = [new TenantId(Guid.NewGuid())]
        };

        Assert.Equal(WorkflowVisibility.Private, sharing.Visibility);
        Assert.Empty(sharing.SharedTenantIds);
        Assert.Equal(ownerTenantId, sharing.OwnerTenantId);
        Assert.Equal(ownerTenantId, sharedCopy.OwnerTenantId);
        Assert.NotSame(sharing, sharedCopy);
    }

    [Fact]
    public void WorkflowSharingId_UsesStrongValueEquality()
    {
        var value = Guid.NewGuid();

        Assert.Equal(new WorkflowSharingId(value), new WorkflowSharingId(value));
        Assert.NotEqual(
            new WorkflowSharingId(value),
            new WorkflowSharingId(Guid.NewGuid()));
    }
}
