using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Tests.Auditing;

public sealed class AuditEntryTests
{
    [Fact]
    public void AuditEntry_IsImmutableAndPreservesIdentityData()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var entry = new AuditEntry
        {
            Id = new AuditEntryId(Guid.NewGuid()),
            UserId = "user-1",
            TenantId = tenantId,
            ResourceTenantId = tenantId,
            Action = "WorkflowDefinition.Create",
            ResourceType = "WorkflowDefinition",
            ResourceIdentifier = "workflow-1:v1",
            Outcome = AuditOutcome.Succeeded,
            CorrelationId = correlationId,
            Timestamp = new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc),
            Metadata = null
        };

        var failedCopy = entry with { Outcome = AuditOutcome.Failed };

        Assert.Equal(AuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal(AuditOutcome.Failed, failedCopy.Outcome);
        Assert.Equal("user-1", entry.UserId);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(tenantId, entry.ResourceTenantId);
        Assert.Equal(correlationId, entry.CorrelationId);
        Assert.Equal("workflow-1:v1", entry.ResourceIdentifier);
        Assert.NotSame(entry, failedCopy);
    }
}
