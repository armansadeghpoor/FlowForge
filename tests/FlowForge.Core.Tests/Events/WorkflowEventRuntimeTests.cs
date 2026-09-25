using System.Text.Json;
using FlowForge.Abstractions.Events;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Engine.Events;

namespace FlowForge.Core.Tests.Events;

public sealed class WorkflowEventRuntimeTests
{
    [Fact]
    public async Task DispatchAsync_MatchingEvent_StartsWorkflow()
    {
        var eventTrigger = CreateEventTrigger();
        var executor = new TriggerExecutorStub();
        var runtime = CreateRuntime([eventTrigger], executor);
        var context = CreateContext();

        var results = await runtime.DispatchAsync(context, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        Assert.Equal(eventTrigger.WorkflowTriggerId, executor.Contexts.Single().TriggerId);
        Assert.Equal(TriggerType.Event, executor.Contexts.Single().TriggerType);
        Assert.Equal(context.CorrelationId, executor.Contexts.Single().CorrelationId);
        Assert.NotEqual(default, executor.Contexts.Single().ExecutionRequestId);
    }

    [Fact]
    public async Task DispatchAsync_MultipleMatches_StartsMultipleExecutions()
    {
        var executor = new TriggerExecutorStub();
        var runtime = CreateRuntime(
            [CreateEventTrigger(), CreateEventTrigger()],
            executor);

        var results = await runtime.DispatchAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.Success));
        Assert.Equal(2, executor.Contexts.Count);
    }

    [Fact]
    public async Task DispatchAsync_MissingDefinition_ReturnsFailedExecution()
    {
        var runtime = CreateRuntime(
            [CreateEventTrigger()],
            new TriggerExecutorStub
            {
                Exception = new KeyNotFoundException("definition missing")
            });

        var results = await runtime.DispatchAsync(
            CreateContext(),
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.Null(result.WorkflowExecutionId);
    }

    [Fact]
    public async Task DispatchAsync_PreservesExecutionIdentity()
    {
        var expected = new WorkflowExecutionId(Guid.NewGuid());
        var runtime = CreateRuntime(
            [CreateEventTrigger()],
            new TriggerExecutorStub { ExecutionId = expected });

        var results = await runtime.DispatchAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.Equal(expected, Assert.Single(results).WorkflowExecutionId);
    }

    private static WorkflowEventRuntime CreateRuntime(
        IReadOnlyList<WorkflowEventTrigger> matches,
        IWorkflowTriggerExecutor executor) =>
        new(new MatcherStub(matches), executor);

    private static WorkflowEventContext CreateContext() =>
        new()
        {
            EventType = "Order.Created",
            Payload = JsonSerializer.SerializeToElement(new { orderId = 42 }),
            OccurredAt = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc),
            CorrelationId = "correlation-42"
        };

    private static WorkflowEventTrigger CreateEventTrigger() =>
        new()
        {
            Id = new WorkflowEventTriggerId(Guid.NewGuid()),
            WorkflowTriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            EventType = "Order.Created",
            Filter = null,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class MatcherStub(IReadOnlyList<WorkflowEventTrigger> matches)
        : IEventTriggerMatcher
    {
        public Task<IReadOnlyList<WorkflowEventTrigger>> FindMatchesAsync(
            WorkflowEventContext context,
            CancellationToken cancellationToken) => Task.FromResult(matches);
    }

    private sealed class TriggerExecutorStub : IWorkflowTriggerExecutor
    {
        public WorkflowExecutionId ExecutionId { get; init; } = new(Guid.NewGuid());

        public Exception? Exception { get; init; }

        public List<WorkflowTriggerExecutionContext> Contexts { get; } = [];

        public Task<WorkflowExecutionId> ExecuteAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            Contexts.Add(context);
            return Exception is null
                ? Task.FromResult(ExecutionId)
                : Task.FromException<WorkflowExecutionId>(Exception);
        }
    }
}
