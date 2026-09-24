using System.Text.Json;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Infrastructure.Triggers;

namespace FlowForge.Infrastructure.Tests.Triggers;

public sealed class InMemoryWorkflowEventTriggerStoreTests
{
    [Fact]
    public async Task SaveAsync_EventTrigger_CanBeRetrieved()
    {
        var store = new InMemoryWorkflowEventTriggerStore();
        var eventTrigger = CreateEventTrigger();

        await store.SaveAsync(eventTrigger, CancellationToken.None);
        var retrieved = await store.GetAsync(eventTrigger.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(eventTrigger.Id, retrieved.Id);
        Assert.Equal(eventTrigger.WorkflowTriggerId, retrieved.WorkflowTriggerId);
        Assert.Equal(eventTrigger.EventType, retrieved.EventType);
        Assert.Equal(eventTrigger.Enabled, retrieved.Enabled);
        Assert.Equal(eventTrigger.CreatedAt, retrieved.CreatedAt);
    }

    [Fact]
    public async Task SaveAsync_MultipleEventTriggers_Coexist()
    {
        var store = new InMemoryWorkflowEventTriggerStore();
        var workflowTriggerId = new WorkflowTriggerId(Guid.NewGuid());
        var orderCreated = CreateEventTrigger(workflowTriggerId, "Order.Created");
        var invoicePaid = CreateEventTrigger(workflowTriggerId, "Invoice.Paid");

        await Task.WhenAll(
            store.SaveAsync(orderCreated, CancellationToken.None),
            store.SaveAsync(invoicePaid, CancellationToken.None));
        var eventTriggers = await store.ListAsync(CancellationToken.None);

        Assert.Equal(2, eventTriggers.Count);
        Assert.Contains(eventTriggers, trigger => trigger.Id == orderCreated.Id);
        Assert.Contains(eventTriggers, trigger => trigger.Id == invoicePaid.Id);
    }

    [Fact]
    public async Task SaveAsync_IsolatesStoredAndReturnedSnapshots()
    {
        var filter = new Dictionary<string, JsonElement>
        {
            ["region"] = JsonSerializer.SerializeToElement("eu")
        };
        var eventTrigger = CreateEventTrigger(filter: filter);
        var store = new InMemoryWorkflowEventTriggerStore();
        await store.SaveAsync(eventTrigger, CancellationToken.None);

        filter["region"] = JsonSerializer.SerializeToElement("changed");
        var firstRead = await store.GetAsync(eventTrigger.Id, CancellationToken.None);
        var secondRead = await store.GetAsync(eventTrigger.Id, CancellationToken.None);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.NotNull(firstRead.Filter);
        Assert.Equal("eu", firstRead.Filter["region"].GetString());
        Assert.NotSame(firstRead, secondRead);
        Assert.NotSame(firstRead.Filter, secondRead.Filter);
    }

    [Fact]
    public async Task SaveAsync_InvalidMetadata_IsRejected()
    {
        var store = new InMemoryWorkflowEventTriggerStore();
        var eventTrigger = CreateEventTrigger();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(eventTrigger with { Id = default }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                eventTrigger with { WorkflowTriggerId = default },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                eventTrigger with { EventType = " " },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                eventTrigger with
                {
                    Filter = new Dictionary<string, JsonElement>
                    {
                        ["invalid"] = default
                    }
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                eventTrigger with { CreatedAt = default },
                CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_PreservesEventTypeExactly()
    {
        var store = new InMemoryWorkflowEventTriggerStore();
        var eventTrigger = CreateEventTrigger(eventType: "Order.Created");

        await store.SaveAsync(eventTrigger, CancellationToken.None);
        var retrieved = await store.GetAsync(eventTrigger.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("Order.Created", retrieved.EventType);
    }

    [Fact]
    public async Task SaveAsync_PreservesFilterMetadata()
    {
        var filter = new Dictionary<string, JsonElement>
        {
            ["customer"] = JsonSerializer.SerializeToElement(new { Tier = "gold" }),
            ["minimumTotal"] = JsonSerializer.SerializeToElement(100)
        };
        var store = new InMemoryWorkflowEventTriggerStore();
        var eventTrigger = CreateEventTrigger(filter: filter);

        await store.SaveAsync(eventTrigger, CancellationToken.None);
        var retrieved = await store.GetAsync(eventTrigger.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Filter);
        Assert.Equal(
            "gold",
            retrieved.Filter["customer"].GetProperty("Tier").GetString());
        Assert.Equal(100, retrieved.Filter["minimumTotal"].GetInt32());
    }

    [Fact]
    public async Task SaveAsync_DuplicateEventTriggerId_IsRejected()
    {
        var store = new InMemoryWorkflowEventTriggerStore();
        var eventTrigger = CreateEventTrigger();
        await store.SaveAsync(eventTrigger, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(eventTrigger, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_MissingEventTrigger_ReturnsNull()
    {
        var store = new InMemoryWorkflowEventTriggerStore();

        var eventTrigger = await store.GetAsync(
            new WorkflowEventTriggerId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(eventTrigger);
    }

    private static WorkflowEventTrigger CreateEventTrigger(
        WorkflowTriggerId? workflowTriggerId = null,
        string eventType = "Order.Created",
        IReadOnlyDictionary<string, JsonElement>? filter = null) =>
        new()
        {
            Id = new WorkflowEventTriggerId(Guid.NewGuid()),
            WorkflowTriggerId = workflowTriggerId ?? new WorkflowTriggerId(Guid.NewGuid()),
            EventType = eventType,
            Filter = filter,
            Enabled = true,
            CreatedAt = new DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc)
        };
}
