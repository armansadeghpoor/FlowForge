using System.Text.Json;
using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Auditing;

namespace FlowForge.Infrastructure.Tests.Auditing;

public sealed class InMemoryAuditStoreTests
{
    [Fact]
    public async Task QueryAsync_EmptyStore_ReturnsEmptyCollection()
    {
        var store = new InMemoryAuditStore();

        var entries = await store.QueryAsync(new AuditQuery(), CancellationToken.None);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task AppendAndQuery_PreservesIdentityAndAppendOrder()
    {
        var store = new InMemoryAuditStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var first = CreateEntry(
            tenantId,
            correlationId,
            "resource-1",
            new DateTime(2026, 9, 26, 11, 0, 0, DateTimeKind.Utc));
        var second = CreateEntry(
            tenantId,
            correlationId,
            "resource-2",
            new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc));

        await store.AppendAsync(first, CancellationToken.None);
        await store.AppendAsync(second, CancellationToken.None);

        var entries = await store.QueryAsync(
            new AuditQuery
            {
                TenantId = tenantId,
                CorrelationId = correlationId,
                ResourceType = "WorkflowDefinition"
            },
            CancellationToken.None);

        Assert.Equal([first.Id, second.Id], entries.Select(entry => entry.Id));
        Assert.All(entries, entry => Assert.Equal("user-1", entry.UserId));
        Assert.All(entries, entry => Assert.Equal(tenantId, entry.TenantId));
        Assert.All(entries, entry => Assert.Equal(tenantId, entry.ResourceTenantId));
        Assert.All(entries, entry => Assert.Equal(correlationId, entry.CorrelationId));
    }

    [Fact]
    public async Task StoredAndReturnedMetadata_AreImmutableSnapshots()
    {
        var store = new InMemoryAuditStore();
        var document = JsonDocument.Parse("{\"safe\":\"value\"}");
        var entry = CreateEntry(
            new TenantId(Guid.NewGuid()),
            new ExecutionCorrelationId(Guid.NewGuid()),
            "resource-1",
            DateTime.UtcNow) with
        {
            Metadata = document.RootElement
        };

        await store.AppendAsync(entry, CancellationToken.None);
        document.Dispose();

        var firstRead = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));
        var secondRead = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));

        Assert.Equal("value", firstRead.Metadata!.Value.GetProperty("safe").GetString());
        Assert.Equal("value", secondRead.Metadata!.Value.GetProperty("safe").GetString());
        Assert.NotSame(firstRead, secondRead);
    }

    [Fact]
    public async Task AppendAsync_ConcurrentEntries_AreAllPreserved()
    {
        var store = new InMemoryAuditStore();
        var tenantId = new TenantId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var entries = Enumerable.Range(0, 100)
            .Select(index => CreateEntry(
                tenantId,
                correlationId,
                $"resource-{index}",
                DateTime.UtcNow))
            .ToArray();

        await Task.WhenAll(entries.Select(entry =>
            store.AppendAsync(entry, CancellationToken.None)));

        var stored = await store.QueryAsync(
            new AuditQuery { TenantId = tenantId },
            CancellationToken.None);

        Assert.Equal(entries.Length, stored.Count);
        Assert.Equal(
            entries.Select(entry => entry.Id).OrderBy(id => id.Value),
            stored.Select(entry => entry.Id).OrderBy(id => id.Value));
    }

    [Fact]
    public async Task AppendAsync_DuplicateIdentity_IsRejectedWithoutReplacement()
    {
        var store = new InMemoryAuditStore();
        var entry = CreateEntry(
            new TenantId(Guid.NewGuid()),
            new ExecutionCorrelationId(Guid.NewGuid()),
            "resource-1",
            DateTime.UtcNow);
        await store.AppendAsync(entry, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(
                entry with { Outcome = AuditOutcome.Failed },
                CancellationToken.None));

        var stored = Assert.Single(
            await store.QueryAsync(new AuditQuery(), CancellationToken.None));
        Assert.Equal(AuditOutcome.Succeeded, stored.Outcome);
    }

    [Fact]
    public async Task QueryAsync_ResourceFilter_IsOrdinalAndExact()
    {
        var store = new InMemoryAuditStore();
        var entry = CreateEntry(
            new TenantId(Guid.NewGuid()),
            new ExecutionCorrelationId(Guid.NewGuid()),
            "resource-1",
            DateTime.UtcNow);
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

        Assert.Single(match);
        Assert.Empty(differentCase);
    }

    private static AuditEntry CreateEntry(
        TenantId tenantId,
        ExecutionCorrelationId correlationId,
        string resourceIdentifier,
        DateTime timestamp) =>
        new()
        {
            Id = new AuditEntryId(Guid.NewGuid()),
            UserId = "user-1",
            TenantId = tenantId,
            ResourceTenantId = tenantId,
            Action = "WorkflowDefinition.Read",
            ResourceType = "WorkflowDefinition",
            ResourceIdentifier = resourceIdentifier,
            Outcome = AuditOutcome.Succeeded,
            CorrelationId = correlationId,
            Timestamp = timestamp,
            Metadata = null
        };
}
