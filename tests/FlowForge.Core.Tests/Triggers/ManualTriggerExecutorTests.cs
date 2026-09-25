using System.Text.Json;
using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Engine;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Engine.Triggers;

namespace FlowForge.Core.Tests.Triggers;

public sealed class ManualTriggerExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_EnabledManualTrigger_ExecutesResolvedDefinition()
    {
        var definition = CreateDefinition();
        var trigger = CreateTrigger(definition);
        var engine = new WorkflowEngineStub(CreateExecution());
        var executor = CreateExecutor(trigger, definition, engine);

        await executor.ExecuteAsync(CreateContext(trigger.Id), CancellationToken.None);

        Assert.Same(definition, engine.ExecutedDefinition);
        Assert.Equal(1, engine.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingTrigger_ThrowsKeyNotFoundException()
    {
        var definition = CreateDefinition();
        var executor = CreateExecutor(null, definition, new WorkflowEngineStub(CreateExecution()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            executor.ExecuteAsync(
                CreateContext(new WorkflowTriggerId(Guid.NewGuid())),
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_DisabledTrigger_ThrowsInvalidOperationException()
    {
        var definition = CreateDefinition();
        var trigger = CreateTrigger(definition) with { Enabled = false };
        var engine = new WorkflowEngineStub(CreateExecution());
        var executor = CreateExecutor(trigger, definition, engine);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(CreateContext(trigger.Id), CancellationToken.None));
        Assert.Equal(0, engine.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDefinitionVersion_ThrowsKeyNotFoundException()
    {
        var definition = CreateDefinition();
        var trigger = CreateTrigger(definition);
        var executor = CreateExecutor(trigger, null, new WorkflowEngineStub(CreateExecution()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            executor.ExecuteAsync(CreateContext(trigger.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsWorkflowEngineExecutionIdentity()
    {
        var definition = CreateDefinition();
        var trigger = CreateTrigger(definition);
        var execution = CreateExecution();
        var executor = CreateExecutor(
            trigger,
            definition,
            new WorkflowEngineStub(execution));

        var executionId = await executor.ExecuteAsync(
            CreateContext(trigger.Id),
            CancellationToken.None);

        Assert.Equal(execution.Id, executionId);
    }

    private static ManualTriggerExecutor CreateExecutor(
        WorkflowTrigger? trigger,
        WorkflowDefinition? definition,
        IWorkflowEngine workflowEngine) =>
        new(
            new TriggerStoreStub(trigger),
            new DefinitionStoreStub(definition),
            workflowEngine);

    private static WorkflowTriggerExecutionContext CreateContext(WorkflowTriggerId triggerId) =>
        new()
        {
            ExecutionRequestId = new ExecutionRequestId(Guid.NewGuid()),
            TriggerId = triggerId,
            TriggerType = TriggerType.Manual,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTime.UtcNow
        };

    private static WorkflowTrigger CreateTrigger(WorkflowDefinition definition) =>
        new()
        {
            Id = new WorkflowTriggerId(Guid.NewGuid()),
            WorkflowDefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            Type = TriggerType.Manual,
            Enabled = true,
            Configuration = new Dictionary<string, JsonElement>()
        };

    private static WorkflowDefinition CreateDefinition() =>
        new()
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            Name = "Manual trigger workflow",
            Version = "1.0",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Nodes = [],
            Edges = []
        };

    private static WorkflowExecution CreateExecution() =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            DefinitionVersion = "1.0",
            Status = WorkflowExecutionStatus.Succeeded,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = []
        };

    private sealed class TriggerStoreStub(WorkflowTrigger? trigger) : IWorkflowTriggerStore
    {
        public Task SaveAsync(WorkflowTrigger value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkflowTrigger?> GetAsync(
            WorkflowTriggerId id,
            CancellationToken cancellationToken) => Task.FromResult(trigger);

        public Task<IReadOnlyList<WorkflowTrigger>> ListAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DefinitionStoreStub(WorkflowDefinition? definition)
        : IWorkflowDefinitionStore
    {
        public Task SaveAsync(WorkflowDefinition value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkflowDefinition?> GetAsync(
            WorkflowDefinitionId id,
            string version,
            CancellationToken cancellationToken) => Task.FromResult(definition);

        public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class WorkflowEngineStub(WorkflowExecution execution) : IWorkflowEngine
    {
        public WorkflowDefinition? ExecutedDefinition { get; private set; }

        public int ExecutionCount { get; private set; }

        public Task<WorkflowExecution> ExecuteAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken)
        {
            ExecutedDefinition = workflow;
            ExecutionCount++;
            return Task.FromResult(execution);
        }
    }
}
