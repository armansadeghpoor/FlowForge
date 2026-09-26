using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Tests.Auditing;
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
        var context = CreateContext();
        var tenantId = new TenantId(Guid.NewGuid());
        var auditStore = new RecordingAuditStore();
        var auditContext = StaticAuditContext.Create(tenantId: tenantId);
        var service = new WorkflowExecutionCommandService(
            executor,
            auditStore,
            auditContext);

        var result = await service.ExecuteTriggerAsync(
            context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        Assert.Same(context, executor.ReceivedContext);
        var audit = Assert.Single(auditStore.Entries);
        Assert.Equal("WorkflowTrigger.Execute", audit.Action);
        Assert.Equal("WorkflowTrigger", audit.ResourceType);
        Assert.Equal(context.TriggerId.Value.ToString("D"), audit.ResourceIdentifier);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(context.CorrelationId, audit.CorrelationId);
        Assert.Equal("user-1", audit.UserId);
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(
            context.ExecutionRequestId.Value,
            audit.Metadata!.Value
                .GetProperty("executionRequestId")
                .GetGuid());
    }

    [Fact]
    public async Task ExecuteTriggerAsync_RuntimeFailure_ReturnsApplicationError()
    {
        var executor = new TriggerExecutorStub
        {
            Exception = new KeyNotFoundException("provider detail")
        };
        var auditStore = new RecordingAuditStore();
        var service = new WorkflowExecutionCommandService(
            executor,
            auditStore,
            StaticAuditContext.Create());

        var result = await service.ExecuteTriggerAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("TriggerExecutionNotFound", error.Code);
        Assert.DoesNotContain("provider detail", error.Message);
        Assert.Equal(
            AuditOutcome.Failed,
            Assert.Single(auditStore.Entries).Outcome);
    }

    [Fact]
    public async Task ExecuteTriggerAsync_DuplicateRequest_ReturnsConflictError()
    {
        var service = new WorkflowExecutionCommandService(
            new TriggerExecutorStub
            {
                Exception = new InvalidOperationException("duplicate detail")
            },
            new RecordingAuditStore(),
            StaticAuditContext.Create());

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
