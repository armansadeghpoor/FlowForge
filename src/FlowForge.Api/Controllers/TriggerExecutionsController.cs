using FlowForge.Abstractions.Triggers;
using FlowForge.Api.Contracts;
using FlowForge.Api.Correlation;
using FlowForge.Api.Errors;
using FlowForge.Application.Common;
using FlowForge.Application.Executions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes manual trigger execution operations.
/// </summary>
[ApiController]
[Route("api/v1/triggers")]
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
        var executionRequestIdResult = GetExecutionRequestId();
        if (executionRequestIdResult.Error is not null)
        {
            return ApplicationErrorMapper.ToActionResult(
                [executionRequestIdResult.Error],
                HttpContext);
        }

        var context = new WorkflowTriggerExecutionContext
        {
            ExecutionRequestId = executionRequestIdResult.Id,
            TriggerId = new WorkflowTriggerId(id),
            TriggerType = TriggerType.Manual,
            CorrelationId = new ExecutionCorrelationId(
                CorrelationIdMiddleware.GetExecutionCorrelationId(HttpContext)),
            RequestedAt = DateTime.UtcNow
        };
        var result = await _service.ExecuteTriggerAsync(context, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new TriggerExecutionDto
            {
                ExecutionRequestId = context.ExecutionRequestId.Value,
                WorkflowExecutionId = result.Value.Value,
                TriggerId = context.TriggerId.Value,
                CorrelationId = context.CorrelationId.Value,
                RequestedAt = context.RequestedAt
            });
    }

    private (ExecutionRequestId Id, ApplicationError? Error)
        GetExecutionRequestId()
    {
        const string headerName = "X-Execution-Request-Id";
        var headerValue = ControllerContext.HttpContext?.Request.Headers[headerName]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return (new ExecutionRequestId(Guid.NewGuid()), null);
        }

        if (!Guid.TryParse(headerValue, out var parsed))
        {
            return (
                default,
                new ApplicationError(
                    "ExecutionRequestIdInvalid",
                    $"The {headerName} header must contain a valid UUID."));
        }

        return (new ExecutionRequestId(parsed), null);
    }
}
