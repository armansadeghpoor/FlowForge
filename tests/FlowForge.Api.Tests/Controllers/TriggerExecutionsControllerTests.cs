using FlowForge.Abstractions.Triggers;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Application.Common;
using FlowForge.Application.Executions;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Tests.Controllers;

public sealed class TriggerExecutionsControllerTests
{
    [Fact]
    public async Task ExecuteAsync_RequestHeader_IsAcceptedAndReturned()
    {
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var requestId = Guid.NewGuid();
        var service = new CommandServiceStub
        {
            Result = ApplicationResult<WorkflowExecutionId>.Success(executionId)
        };
        var controller = CreateController(service, requestId);
        var triggerId = Guid.NewGuid();

        var result = await controller.ExecuteAsync(
            triggerId,
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<TriggerExecutionDto>(created.Value);
        Assert.Equal(requestId, response.ExecutionRequestId);
        Assert.Equal(executionId.Value, response.WorkflowExecutionId);
        Assert.Equal(triggerId, response.TriggerId);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Equal(service.ReceivedContext!.RequestedAt, response.RequestedAt);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateRequest_ReturnsConflict()
    {
        var service = new DuplicateAwareCommandServiceStub();
        var controller = CreateController(service, Guid.NewGuid());

        var first = await controller.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);
        var duplicate = await controller.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<ObjectResult>(first);
        Assert.IsType<ConflictObjectResult>(duplicate);
        Assert.Single(service.RegisteredRequests);
    }

    [Fact]
    public async Task ExecuteAsync_NoRequestHeader_GeneratesRequestIdentity()
    {
        var service = new CommandServiceStub
        {
            Result = ApplicationResult<WorkflowExecutionId>.Success(
                new WorkflowExecutionId(Guid.NewGuid()))
        };
        var controller = CreateController(service, null);

        var result = await controller.ExecuteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.NotEqual(default, service.ReceivedContext!.ExecutionRequestId);
    }

    private static TriggerExecutionsController CreateController(
        IWorkflowExecutionCommandService service,
        Guid? requestId)
    {
        var controller = new TriggerExecutionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        if (requestId is not null)
        {
            controller.Request.Headers["X-Execution-Request-Id"] = requestId.Value.ToString();
        }

        return controller;
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

    private sealed class DuplicateAwareCommandServiceStub : IWorkflowExecutionCommandService
    {
        public HashSet<ExecutionRequestId> RegisteredRequests { get; } = [];

        public Task<ApplicationResult<WorkflowExecutionId>> ExecuteTriggerAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            var result = RegisteredRequests.Add(context.ExecutionRequestId)
                ? ApplicationResult<WorkflowExecutionId>.Success(
                    new WorkflowExecutionId(Guid.NewGuid()))
                : ApplicationResult<WorkflowExecutionId>.Failure(
                    new ApplicationError(
                        "TriggerExecutionConflict",
                        "The execution request is already registered."));
            return Task.FromResult(result);
        }
    }
}
