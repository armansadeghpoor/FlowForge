using FlowForge.Api.Contracts;
using FlowForge.Api.Errors;
using FlowForge.Api.Mapping;
using FlowForge.Application.Triggers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes workflow trigger management operations.
/// </summary>
[ApiController]
[Route("api/triggers")]
public sealed class TriggersController : ControllerBase
{
    private readonly IWorkflowTriggerService _service;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public TriggersController(IWorkflowTriggerService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>
    /// Lists workflow triggers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors);
        }

        return Ok(result.Value!.Select(trigger => trigger.ToDto()).ToArray());
    }

    /// <summary>
    /// Creates a workflow trigger.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateWorkflowTriggerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request.ToDomain(), cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            result.Value!.ToDto());
    }
}
