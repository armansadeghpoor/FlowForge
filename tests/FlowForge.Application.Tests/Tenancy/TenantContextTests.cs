using FlowForge.Abstractions.Tenancy;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests.Tenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void None_HasNoAvailableTenant()
    {
        ITenantContext context = TenantContext.None;

        Assert.False(context.HasTenant);
        Assert.Null(context.TenantId);
    }

    [Fact]
    public void ApplicationConsumer_CanReadAvailableTenantContext()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var consumer = new TenantAwareApplicationConsumer(new TenantContext(tenantId));

        Assert.True(consumer.HasTenant);
        Assert.Equal(tenantId, consumer.TenantId);
    }

    private sealed class TenantAwareApplicationConsumer(ITenantContext tenantContext)
    {
        public TenantId? TenantId => tenantContext.TenantId;

        public bool HasTenant => tenantContext.HasTenant;
    }
}
