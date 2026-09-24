using System.Text.Json;
using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Triggers;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Tests;

public sealed class EventTriggerServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidEventTrigger_IsPersisted()
    {
        var eventTrigger = CreateEventTrigger();
        var store = new FakeEventTriggerStore();
        var service = new WorkflowEventTriggerService(store);

        var result = await service.CreateAsync(eventTrigger, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(eventTrigger, result.Value);
        Assert.Same(eventTrigger, store.SavedEventTrigger);
    }

    [Fact]
    public async Task CreateAsync_InvalidEventTrigger_IsRejectedWithoutPersistence()
    {
        var store = new FakeEventTriggerStore();
        var service = new WorkflowEventTriggerService(store);
        var eventTrigger = CreateEventTrigger() with
        {
            Id = default,
            WorkflowTriggerId = default,
            EventType = " ",
            Filter = new Dictionary<string, JsonElement> { ["invalid"] = default },
            CreatedAt = default
        };

        var result = await service.CreateAsync(eventTrigger, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(store.SavedEventTrigger);
        Assert.Equal(5, result.Errors.Count);
    }

    [Fact]
    public async Task GetAsync_ExistingEventTrigger_IsReturned()
    {
        var eventTrigger = CreateEventTrigger();
        var service = new WorkflowEventTriggerService(
            new FakeEventTriggerStore { EventTriggerToReturn = eventTrigger });

        var result = await service.GetAsync(eventTrigger.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(eventTrigger, result.Value);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEventTrigger_ReturnsApplicationError()
    {
        var store = new FakeEventTriggerStore
        {
            SaveException = new InvalidOperationException("provider detail")
        };
        var service = new WorkflowEventTriggerService(store);

        var result = await service.CreateAsync(
            CreateEventTrigger(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("EventTriggerAlreadyExists", error.Code);
        Assert.DoesNotContain("provider detail", error.Message);
    }

    private static WorkflowEventTrigger CreateEventTrigger() =>
        new()
        {
            Id = new WorkflowEventTriggerId(Guid.NewGuid()),
            WorkflowTriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            EventType = "Order.Created",
            Filter = new Dictionary<string, JsonElement>
            {
                ["region"] = JsonSerializer.SerializeToElement("eu")
            },
            Enabled = true,
            CreatedAt = new DateTime(2026, 9, 24, 17, 0, 0, DateTimeKind.Utc)
        };

    private sealed class FakeEventTriggerStore : IWorkflowEventTriggerStore
    {
        public WorkflowEventTrigger? SavedEventTrigger { get; private set; }

        public WorkflowEventTrigger? EventTriggerToReturn { get; init; }

        public Exception? SaveException { get; init; }

        public Task SaveAsync(
            WorkflowEventTrigger eventTrigger,
            CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedEventTrigger = eventTrigger;
            return Task.CompletedTask;
        }

        public Task<WorkflowEventTrigger?> GetAsync(
            WorkflowEventTriggerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(EventTriggerToReturn);

        public Task<IReadOnlyList<WorkflowEventTrigger>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowEventTrigger>>(
                EventTriggerToReturn is null ? [] : [EventTriggerToReturn]);
    }
}
