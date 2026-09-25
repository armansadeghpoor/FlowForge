using FlowForge.Api.Errors;
using FlowForge.Api.Mapping;
using FlowForge.Application.Queries;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes read-only workflow execution visibility operations.
/// </summary>
[ApiController]
[Route("api/v1/executions")]
public sealed class ExecutionsController : ControllerBase
{
    private readonly IWorkflowExecutionQueryService _service;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public ExecutionsController(IWorkflowExecutionQueryService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>
    /// Finds executions sharing a correlation identifier.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FindExecutionsByCorrelationIdAsync(
        [FromQuery] Guid correlationId,
        CancellationToken cancellationToken)
    {
        var result = await _service.FindExecutionsByCorrelationIdAsync(
            new ExecutionCorrelationId(correlationId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        return Ok(result.Value!.Select(summary => summary.ToDto()).ToArray());
    }

    /// <summary>
    /// Gets an execution summary.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSummaryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetSummaryAsync(
            new WorkflowExecutionId(id),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        return Ok(result.Value!.ToDto());
    }

    /// <summary>
    /// Gets an ordered execution timeline.
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimelineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetTimelineAsync(
            new WorkflowExecutionId(id),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        return Ok(result.Value!.Select(entry => entry.ToDto()).ToArray());
    }
}
