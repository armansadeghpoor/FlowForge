using System.Text.Json;
using FlowForge.Abstractions.Events;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Application.Common;
using FlowForge.Application.Events;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Tests.Controllers;

public sealed class EventsControllerTests
{
    [Fact]
    public async Task DispatchAsync_ValidEvent_ReturnsExecutionResults()
    {
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var service = new EventServiceStub
        {
            Result = ApplicationResult<IReadOnlyList<EventExecutionResult>>.Success(
            [
                new EventExecutionResult
                {
                    EventTriggerId = new WorkflowEventTriggerId(Guid.NewGuid()),
                    TriggerId = new WorkflowTriggerId(Guid.NewGuid()),
                    Success = true,
                    WorkflowExecutionId = executionId
                }
            ])
        };
        var controller = new EventsController(service);

        var result = await controller.DispatchAsync(
            CreateRequest("Order.Created"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WorkflowEventDispatchDto>(ok.Value);
        Assert.Equal("Order.Created", response.EventType);
        Assert.Equal(executionId.Value, Assert.Single(response.Executions).WorkflowExecutionId);
        Assert.Equal(service.Context!.CorrelationId, response.CorrelationId);
    }

    [Fact]
    public async Task DispatchAsync_InvalidRequest_ReturnsBadRequest()
    {
        var service = new EventServiceStub
        {
            Result = ApplicationResult<IReadOnlyList<EventExecutionResult>>.Failure(
                new ApplicationError("EventTypeRequired", "An event type is required."))
        };
        var controller = new EventsController(service);

        var result = await controller.DispatchAsync(
            CreateRequest(string.Empty),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static DispatchWorkflowEventRequest CreateRequest(string eventType) =>
        new()
        {
            EventType = eventType,
            Payload = JsonSerializer.SerializeToElement(new { orderId = 42 })
        };

    private sealed class EventServiceStub : IWorkflowEventService
    {
        public required ApplicationResult<IReadOnlyList<EventExecutionResult>> Result
        {
            get;
            init;
        }

        public WorkflowEventContext? Context { get; private set; }

        public Task<ApplicationResult<IReadOnlyList<EventExecutionResult>>> DispatchAsync(
            WorkflowEventContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            return Task.FromResult(Result);
        }
    }
}
