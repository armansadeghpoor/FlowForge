using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Tenants;

namespace FlowForge.Core.Tests.Tenancy;

public sealed class TenantTests
{
    [Fact]
    public void TenantId_UsesStrongValueEquality()
    {
        var value = Guid.NewGuid();

        Assert.Equal(new TenantId(value), new TenantId(value));
        Assert.NotEqual(new TenantId(value), new TenantId(Guid.NewGuid()));
    }

    [Fact]
    public void Tenant_IsImmutableRecord()
    {
        var original = new Tenant
        {
            Id = new TenantId(Guid.NewGuid()),
            Name = "Original tenant"
        };

        var renamed = original with { Name = "Renamed tenant" };

        Assert.Equal("Original tenant", original.Name);
        Assert.Equal("Renamed tenant", renamed.Name);
        Assert.Equal(original.Id, renamed.Id);
    }
}
