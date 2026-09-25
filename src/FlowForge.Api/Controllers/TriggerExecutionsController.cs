using FlowForge.Abstractions.Triggers;
using FlowForge.Api.Contracts;
using FlowForge.Api.Errors;
using FlowForge.Application.Executions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes manual trigger execution operations.
/// </summary>
[ApiController]
[Route("api/triggers")]
public sealed class TriggerExecutionsController : ControllerBase
{
    private readonly IWorkflowExecutionCommandService _service;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public TriggerExecutionsController(IWorkflowExecutionCommandService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>
    /// Executes a manual workflow trigger.
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = new WorkflowTriggerExecutionContext
        {
            TriggerId = new WorkflowTriggerId(id),
            TriggerType = TriggerType.Manual,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTime.UtcNow
        };
        var result = await _service.ExecuteTriggerAsync(context, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new TriggerExecutionDto
            {
                WorkflowExecutionId = result.Value.Value,
                TriggerId = context.TriggerId.Value,
                CorrelationId = context.CorrelationId,
                RequestedAt = context.RequestedAt
            });
    }
}
