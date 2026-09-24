using System.Text.Json;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Infrastructure.Triggers;

namespace FlowForge.Infrastructure.Tests.Triggers;

public sealed class InMemoryWorkflowTriggerStoreTests
{
    [Fact]
    public async Task SaveAsync_Trigger_CanBeRetrieved()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var trigger = CreateTrigger();

        await store.SaveAsync(trigger, CancellationToken.None);
        var retrieved = await store.GetAsync(trigger.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(trigger.Id, retrieved.Id);
        Assert.Equal(trigger.WorkflowDefinitionId, retrieved.WorkflowDefinitionId);
        Assert.Equal(trigger.DefinitionVersion, retrieved.DefinitionVersion);
        Assert.Equal(trigger.Type, retrieved.Type);
        Assert.Equal("0 0 * * *", retrieved.Configuration["schedule"].GetString());
    }

    [Fact]
    public async Task SaveAsync_MultipleTriggers_Coexist()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var definitionId = new WorkflowDefinitionId(Guid.NewGuid());
        var first = CreateTrigger(
            definitionId: definitionId,
            definitionVersion: "v1",
            type: TriggerType.Manual);
        var second = CreateTrigger(
            definitionId: definitionId,
            definitionVersion: "v1",
            type: TriggerType.Event);
        var third = CreateTrigger(
            definitionId: definitionId,
            definitionVersion: "v2",
            type: TriggerType.Timer);

        await Task.WhenAll(
            store.SaveAsync(first, CancellationToken.None),
            store.SaveAsync(second, CancellationToken.None),
            store.SaveAsync(third, CancellationToken.None));
        var triggers = await store.ListAsync(CancellationToken.None);

        Assert.Equal(3, triggers.Count);
        Assert.Contains(triggers, trigger => trigger.Id == first.Id);
        Assert.Contains(triggers, trigger => trigger.Id == second.Id);
        Assert.Contains(triggers, trigger => trigger.Id == third.Id);
    }

    [Fact]
    public async Task SaveAsync_IsolatesStoredAndReturnedSnapshots()
    {
        var configuration = new Dictionary<string, JsonElement>
        {
            ["schedule"] = JsonSerializer.SerializeToElement("0 0 * * *")
        };
        var trigger = CreateTrigger(configuration: configuration);
        var store = new InMemoryWorkflowTriggerStore();
        await store.SaveAsync(trigger, CancellationToken.None);

        configuration["schedule"] = JsonSerializer.SerializeToElement("changed");
        var firstRead = await store.GetAsync(trigger.Id, CancellationToken.None);
        var secondRead = await store.GetAsync(trigger.Id, CancellationToken.None);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal("0 0 * * *", firstRead.Configuration["schedule"].GetString());
        Assert.NotSame(firstRead, secondRead);
        Assert.NotSame(firstRead.Configuration, secondRead.Configuration);
    }

    [Fact]
    public async Task GetAsync_MissingTrigger_ReturnsNull()
    {
        var store = new InMemoryWorkflowTriggerStore();

        var trigger = await store.GetAsync(
            new WorkflowTriggerId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(trigger);
    }

    [Fact]
    public async Task SaveAsync_PreservesDefinitionAndVersionReference()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var definitionId = new WorkflowDefinitionId(Guid.NewGuid());
        var trigger = CreateTrigger(
            definitionId: definitionId,
            definitionVersion: "release-42");

        await store.SaveAsync(trigger, CancellationToken.None);
        var retrieved = await store.GetAsync(trigger.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(definitionId, retrieved.WorkflowDefinitionId);
        Assert.Equal("release-42", retrieved.DefinitionVersion);
    }

    [Fact]
    public async Task SaveAsync_MissingTriggerId_IsRejected()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var trigger = CreateTrigger() with { Id = default };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(trigger, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_MissingDefinitionId_IsRejected()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var trigger = CreateTrigger() with { WorkflowDefinitionId = default };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(trigger, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_MissingDefinitionVersion_IsRejected()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var trigger = CreateTrigger() with { DefinitionVersion = " " };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(trigger, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_UndefinedTriggerType_IsRejected()
    {
        var store = new InMemoryWorkflowTriggerStore();
        var trigger = CreateTrigger() with { Type = (TriggerType)999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(trigger, CancellationToken.None));
    }

    private static WorkflowTrigger CreateTrigger(
        WorkflowDefinitionId? definitionId = null,
        string definitionVersion = "v1",
        TriggerType type = TriggerType.Timer,
        IReadOnlyDictionary<string, JsonElement>? configuration = null) =>
        new()
        {
            Id = new WorkflowTriggerId(Guid.NewGuid()),
            WorkflowDefinitionId = definitionId ?? new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = definitionVersion,
            Type = type,
            Configuration = configuration ?? new Dictionary<string, JsonElement>
            {
                ["schedule"] = JsonSerializer.SerializeToElement("0 0 * * *")
            }
        };
}
