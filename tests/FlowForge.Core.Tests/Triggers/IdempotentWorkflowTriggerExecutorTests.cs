using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Triggers;
using FlowForge.Infrastructure.Execution;

namespace FlowForge.Core.Tests.Triggers;

public sealed class IdempotentWorkflowTriggerExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_FirstRequest_ExecutesInnerExecutor()
    {
        var inner = new TriggerExecutorStub();
        var executor = CreateExecutor(inner);

        var executionId = await executor.ExecuteAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.Equal(inner.ExecutionId, executionId);
        Assert.Equal(1, inner.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_SameRequest_ExecutesOnlyOnceAndRejectsDuplicate()
    {
        var inner = new TriggerExecutorStub();
        var executor = CreateExecutor(inner);
        var context = CreateContext();

        await executor.ExecuteAsync(context, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(context, CancellationToken.None));
        Assert.Equal(1, inner.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExecutionRequestIdentity()
    {
        var inner = new TriggerExecutorStub();
        var executor = CreateExecutor(inner);
        var context = CreateContext();

        await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Same(context, inner.Context);
        Assert.Equal(context.ExecutionRequestId, inner.Context!.ExecutionRequestId);
    }

    private static IdempotentWorkflowTriggerExecutor CreateExecutor(
        IWorkflowTriggerExecutor inner) =>
        new(new InMemoryExecutionRequestStore(), inner);

    private static WorkflowTriggerExecutionContext CreateContext() =>
        new()
        {
            ExecutionRequestId = new ExecutionRequestId(Guid.NewGuid()),
            TriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            TriggerType = TriggerType.Manual,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTime.UtcNow
        };

    private sealed class TriggerExecutorStub : IWorkflowTriggerExecutor
    {
        public WorkflowExecutionId ExecutionId { get; } = new(Guid.NewGuid());

        public int ExecutionCount { get; private set; }

        public WorkflowTriggerExecutionContext? Context { get; private set; }

        public Task<WorkflowExecutionId> ExecuteAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            Context = context;
            return Task.FromResult(ExecutionId);
        }
    }
}
