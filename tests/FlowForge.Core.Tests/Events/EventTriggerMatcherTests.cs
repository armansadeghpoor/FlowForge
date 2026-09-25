using System.Text.Json;
using FlowForge.Abstractions.Events;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Engine.Events;

namespace FlowForge.Core.Tests.Events;

public sealed class EventTriggerMatcherTests
{
    [Fact]
    public async Task FindMatchesAsync_MatchingEventType_ReturnsTrigger()
    {
        var matching = CreateEventTrigger("Order.Created");
        var matcher = new EventTriggerMatcher(
            new EventTriggerStoreStub([matching, CreateEventTrigger("Invoice.Paid")]));

        var matches = await matcher.FindMatchesAsync(
            CreateContext("Order.Created"),
            CancellationToken.None);

        Assert.Equal(matching.Id, Assert.Single(matches).Id);
    }

    [Fact]
    public async Task FindMatchesAsync_NoMatchingTrigger_ReturnsEmptyCollection()
    {
        var matcher = new EventTriggerMatcher(
            new EventTriggerStoreStub([CreateEventTrigger("Invoice.Paid")]));

        var matches = await matcher.FindMatchesAsync(
            CreateContext("Order.Created"),
            CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_MultipleMatchingTriggers_ReturnsAllMatches()
    {
        var first = CreateEventTrigger("Order.Created");
        var second = CreateEventTrigger("Order.Created");
        var matcher = new EventTriggerMatcher(
            new EventTriggerStoreStub([first, CreateEventTrigger("order.created"), second]));

        var matches = await matcher.FindMatchesAsync(
            CreateContext("Order.Created"),
            CancellationToken.None);

        Assert.Equal([first.Id, second.Id], matches.Select(match => match.Id));
    }

    private static WorkflowEventContext CreateContext(string eventType) =>
        new()
        {
            EventType = eventType,
            Payload = JsonSerializer.SerializeToElement(new { orderId = 42 }),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static WorkflowEventTrigger CreateEventTrigger(string eventType) =>
        new()
        {
            Id = new WorkflowEventTriggerId(Guid.NewGuid()),
            WorkflowTriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            EventType = eventType,
            Filter = null,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class EventTriggerStoreStub(
        IReadOnlyList<WorkflowEventTrigger> eventTriggers) : IWorkflowEventTriggerStore
    {
        public Task SaveAsync(
            WorkflowEventTrigger eventTrigger,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WorkflowEventTrigger?> GetAsync(
            WorkflowEventTriggerId id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkflowEventTrigger>> ListAsync(
            CancellationToken cancellationToken) => Task.FromResult(eventTriggers);
    }
}
