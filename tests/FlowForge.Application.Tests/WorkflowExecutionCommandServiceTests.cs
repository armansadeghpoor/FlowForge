using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Executions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests;

public sealed class WorkflowExecutionCommandServiceTests
{
    [Fact]
    public async Task ExecuteTriggerAsync_Success_ReturnsExecutionIdentity()
    {
        var expected = new WorkflowExecutionId(Guid.NewGuid());
        var executor = new TriggerExecutorStub { ExecutionId = expected };
        var service = new WorkflowExecutionCommandService(executor);
        var context = CreateContext();

        var result = await service.ExecuteTriggerAsync(
            context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        Assert.Same(context, executor.ReceivedContext);
    }

    [Fact]
    public async Task ExecuteTriggerAsync_RuntimeFailure_ReturnsApplicationError()
    {
        var executor = new TriggerExecutorStub
        {
            Exception = new KeyNotFoundException("provider detail")
        };
        var service = new WorkflowExecutionCommandService(executor);

        var result = await service.ExecuteTriggerAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("TriggerExecutionNotFound", error.Code);
        Assert.DoesNotContain("provider detail", error.Message);
    }

    [Fact]
    public async Task ExecuteTriggerAsync_DuplicateRequest_ReturnsConflictError()
    {
        var service = new WorkflowExecutionCommandService(
            new TriggerExecutorStub
            {
                Exception = new InvalidOperationException("duplicate detail")
            });

        var result = await service.ExecuteTriggerAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("TriggerExecutionConflict", error.Code);
        Assert.DoesNotContain("duplicate detail", error.Message);
    }

    private static WorkflowTriggerExecutionContext CreateContext() =>
        new()
        {
            ExecutionRequestId = new ExecutionRequestId(Guid.NewGuid()),
            TriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            TriggerType = TriggerType.Manual,
            CorrelationId = new ExecutionCorrelationId(Guid.NewGuid()),
            RequestedAt = DateTime.UtcNow
        };

    private sealed class TriggerExecutorStub : IWorkflowTriggerExecutor
    {
        public WorkflowExecutionId ExecutionId { get; init; } =
            new(Guid.NewGuid());

        public Exception? Exception { get; init; }

        public WorkflowTriggerExecutionContext? ReceivedContext { get; private set; }

        public Task<WorkflowExecutionId> ExecuteAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            ReceivedContext = context;
            return Exception is null
                ? Task.FromResult(ExecutionId)
                : Task.FromException<WorkflowExecutionId>(Exception);
        }
    }
}
