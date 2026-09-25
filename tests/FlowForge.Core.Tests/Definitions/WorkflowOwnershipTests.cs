using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Tests.Definitions;

public sealed class WorkflowOwnershipTests
{
    [Fact]
    public void WorkflowDefinition_PreservesImmutableTenantOwnershipMetadata()
    {
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var definition = new WorkflowDefinition
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = ownerTenantId,
            Name = "Owned workflow",
            Version = "1.0",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Nodes = [],
            Edges = []
        };

        var reassignedCopy = definition with
        {
            OwnerTenantId = new TenantId(Guid.NewGuid())
        };

        Assert.Equal(ownerTenantId, definition.OwnerTenantId);
        Assert.NotEqual(definition.OwnerTenantId, reassignedCopy.OwnerTenantId);
        Assert.NotSame(definition, reassignedCopy);
    }
}
