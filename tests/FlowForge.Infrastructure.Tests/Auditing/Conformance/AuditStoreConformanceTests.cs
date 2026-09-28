using System.Text.Json;
using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Tests.Auditing.Conformance;

public abstract class AuditStoreConformanceTests
{
    protected abstract IAuditStore CreateStore();

    [SkippableFact]
    public async Task AppendAndQuery_PreservesAllAuditFields()
    {
        var store = CreateStore();
        var requestTenantId = new TenantId(Guid.NewGuid());
        var resourceTenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var timestamp = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Utc);
        var metadata = JsonSerializer.Deserialize<JsonElement>(
            "{\"operation\":\"create\",\"attempt\":1}");
        var entry = CreateEntry(
            requestTenantId,
            resourceTenantId,
            correlationId,
            "workflow-42",
            timestamp) with
        {
            UserId = "user-7",
            Action = "WorkflowDefinition.Create",
            Outcome = AuditOutcome.Denied,
            Metadata = metadata
        };

        await store.AppendAsync(entry, CancellationToken.None);
        var stored = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));

        Assert.Equal(entry.Id, stored.Id);
        Assert.Equal("user-7", stored.UserId);
        Assert.Equal(requestTenantId, stored.TenantId);
        Assert.Equal(resourceTenantId, stored.ResourceTenantId);
        Assert.Equal("WorkflowDefinition.Create", stored.Action);
        Assert.Equal(entry.ResourceType, stored.ResourceType);
        Assert.Equal("workflow-42", stored.ResourceIdentifier);
        Assert.Equal(AuditOutcome.Denied, stored.Outcome);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Equal(timestamp, stored.Timestamp);
        Assert.Equal(
            "create",
            stored.Metadata!.Value.GetProperty("operation").GetString());
        Assert.Equal(1, stored.Metadata.Value.GetProperty("attempt").GetInt32());
    }

    [SkippableFact]
    public async Task QueryAsync_EmptyStore_ReturnsEmptyCollection()
    {
        var entries = await CreateStore().QueryAsync(
            new AuditQuery(),
            CancellationToken.None);

        Assert.Empty(entries);
    }

    [SkippableFact]
    public async Task QueryAsync_TenantFilter_MatchesRequestOrResourceTenant()
    {
        var store = CreateStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var otherTenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var requestTenantEntry = CreateEntry(
            tenantId,
            otherTenantId,
            correlationId,
            "request-tenant",
            UtcTimestamp);
        var resourceTenantEntry = CreateEntry(
            otherTenantId,
            tenantId,
            correlationId,
            "resource-tenant",
            UtcTimestamp.AddMinutes(1));
        var unrelatedEntry = CreateEntry(
            otherTenantId,
            otherTenantId,
            correlationId,
            "unrelated",
            UtcTimestamp.AddMinutes(2));
        await store.AppendAsync(requestTenantEntry, CancellationToken.None);
        await store.AppendAsync(resourceTenantEntry, CancellationToken.None);
        await store.AppendAsync(unrelatedEntry, CancellationToken.None);

        var entries = await store.QueryAsync(
            new AuditQuery { TenantId = tenantId },
            CancellationToken.None);

        Assert.Equal(
            [requestTenantEntry.Id, resourceTenantEntry.Id],
            entries.Select(entry => entry.Id));
    }

    [SkippableFact]
    public async Task QueryAsync_CorrelationFilter_ReturnsOnlyMatches()
    {
        var store = CreateStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var matching = CreateEntry(
            tenantId,
            tenantId,
            correlationId,
            "matching",
            UtcTimestamp);
        var unrelated = CreateEntry(
            tenantId,
            tenantId,
            new ExecutionCorrelationId(Guid.NewGuid()),
            "unrelated",
            UtcTimestamp);
        await store.AppendAsync(matching, CancellationToken.None);
        await store.AppendAsync(unrelated, CancellationToken.None);

        var entries = await store.QueryAsync(
            new AuditQuery { CorrelationId = correlationId },
            CancellationToken.None);

        Assert.Equal(matching.Id, Assert.Single(entries).Id);
    }

    [SkippableFact]
    public async Task QueryAsync_ResourceFilter_IsExactAndOrdinal()
    {
        var store = CreateStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var entry = CreateEntry(
            tenantId,
            tenantId,
            new ExecutionCorrelationId(Guid.NewGuid()),
            "workflow-1",
            UtcTimestamp);
        await store.AppendAsync(entry, CancellationToken.None);

        var match = await store.QueryAsync(
            new AuditQuery
            {
                ResourceType = entry.ResourceType,
                ResourceIdentifier = entry.ResourceIdentifier
            },
            CancellationToken.None);
        var differentCase = await store.QueryAsync(
            new AuditQuery
            {
                ResourceType = entry.ResourceType.ToLowerInvariant(),
                ResourceIdentifier = entry.ResourceIdentifier
            },
            CancellationToken.None);

        Assert.Equal(entry.Id, Assert.Single(match).Id);
        Assert.Empty(differentCase);
    }

    [SkippableFact]
    public async Task AppendAndQuery_MetadataIsAnImmutableSnapshot()
    {
        var store = CreateStore();
        var document = JsonDocument.Parse("{\"safe\":\"value\"}");
        var tenantId = new TenantId(Guid.NewGuid());
        var entry = CreateEntry(
            tenantId,
            tenantId,
            new ExecutionCorrelationId(Guid.NewGuid()),
            "workflow-1",
            UtcTimestamp) with
        {
            Metadata = document.RootElement
        };

        await store.AppendAsync(entry, CancellationToken.None);
        document.Dispose();
        var firstRead = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));
        var secondRead = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));

        Assert.Equal(
            "value",
            firstRead.Metadata!.Value.GetProperty("safe").GetString());
        Assert.Equal(
            "value",
            secondRead.Metadata!.Value.GetProperty("safe").GetString());
        Assert.NotSame(firstRead, secondRead);
    }

    [SkippableFact]
    public async Task AppendAsync_DuplicateIdentity_IsRejectedWithoutReplacement()
    {
        var store = CreateStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var entry = CreateEntry(
            tenantId,
            tenantId,
            new ExecutionCorrelationId(Guid.NewGuid()),
            "workflow-1",
            UtcTimestamp);
        await store.AppendAsync(entry, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(
                entry with { Outcome = AuditOutcome.Failed },
                CancellationToken.None));

        var stored = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));
        Assert.Equal(AuditOutcome.Succeeded, stored.Outcome);
    }

    [SkippableFact]
    public async Task QueryAsync_PreservesDeterministicAppendOrder()
    {
        var store = CreateStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var first = CreateEntry(
            tenantId,
            tenantId,
            correlationId,
            "first",
            UtcTimestamp.AddHours(1));
        var second = CreateEntry(
            tenantId,
            tenantId,
            correlationId,
            "second",
            UtcTimestamp);
        await store.AppendAsync(first, CancellationToken.None);
        await store.AppendAsync(second, CancellationToken.None);

        var entries = await store.QueryAsync(
            new AuditQuery(),
            CancellationToken.None);

        Assert.Equal([first.Id, second.Id], entries.Select(entry => entry.Id));
    }

    private static readonly DateTime UtcTimestamp =
        new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    private static AuditEntry CreateEntry(
        TenantId tenantId,
        TenantId resourceTenantId,
        ExecutionCorrelationId correlationId,
        string resourceIdentifier,
        DateTime timestamp) =>
        new()
        {
            Id = new AuditEntryId(Guid.NewGuid()),
            UserId = "user-1",
            TenantId = tenantId,
            ResourceTenantId = resourceTenantId,
            Action = "WorkflowDefinition.Read",
            ResourceType = "WorkflowDefinition",
            ResourceIdentifier = resourceIdentifier,
            Outcome = AuditOutcome.Succeeded,
            CorrelationId = correlationId,
            Timestamp = timestamp,
            Metadata = null
        };
}
