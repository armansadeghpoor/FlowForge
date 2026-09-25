using FlowForge.Abstractions.Events;
using FlowForge.Api.Contracts;
using FlowForge.Api.Errors;
using FlowForge.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes external workflow event dispatch operations.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly IWorkflowEventService _service;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public EventsController(IWorkflowEventService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>
    /// Dispatches an external event to matching workflow triggers.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DispatchAsync(
        [FromBody] DispatchWorkflowEventRequest request,
        CancellationToken cancellationToken)
    {
        var context = new WorkflowEventContext
        {
            EventType = request.EventType,
            Payload = request.Payload.Clone(),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        var result = await _service.DispatchAsync(context, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors);
        }

        return Ok(new WorkflowEventDispatchDto
        {
            EventType = context.EventType,
            CorrelationId = context.CorrelationId,
            OccurredAt = context.OccurredAt,
            Executions = Array.AsReadOnly(result.Value!
                .Select(execution => new EventExecutionResultDto
                {
                    EventTriggerId = execution.EventTriggerId.Value,
                    TriggerId = execution.TriggerId.Value,
                    Success = execution.Success,
                    WorkflowExecutionId = execution.WorkflowExecutionId?.Value
                })
                .ToArray())
        });
    }
}
