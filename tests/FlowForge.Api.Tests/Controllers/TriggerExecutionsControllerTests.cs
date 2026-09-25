using FlowForge.Abstractions.Triggers;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Application.Common;
using FlowForge.Application.Executions;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Tests.Controllers;

public sealed class TriggerExecutionsControllerTests
{
    [Fact]
    public async Task ExecuteAsync_ManualTrigger_ReturnsCreatedExecution()
    {
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var service = new CommandServiceStub
        {
            Result = ApplicationResult<WorkflowExecutionId>.Success(executionId)
        };
        var controller = new TriggerExecutionsController(service);
        var triggerId = Guid.NewGuid();

        var result = await controller.ExecuteAsync(
            triggerId,
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<TriggerExecutionDto>(created.Value);
        Assert.Equal(executionId.Value, response.WorkflowExecutionId);
        Assert.Equal(triggerId, response.TriggerId);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Equal(service.ReceivedContext!.RequestedAt, response.RequestedAt);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTriggerExecution_ReturnsConflict()
    {
        var service = new CommandServiceStub
        {
            Result = ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionConflict",
                    "The trigger cannot be executed in its current state."))
        };
        var controller = new TriggerExecutionsController(service);

        var result = await controller.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    private sealed class CommandServiceStub : IWorkflowExecutionCommandService
    {
        public required ApplicationResult<WorkflowExecutionId> Result { get; init; }

        public WorkflowTriggerExecutionContext? ReceivedContext { get; private set; }

        public Task<ApplicationResult<WorkflowExecutionId>> ExecuteTriggerAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            ReceivedContext = context;
            return Task.FromResult(Result);
        }
    }
}
