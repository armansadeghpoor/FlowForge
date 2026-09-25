using FlowForge.Abstractions.Security;
using FlowForge.Api.Contracts;
using FlowForge.Api.Errors;
using FlowForge.Api.Mapping;
using FlowForge.Application.Common;
using FlowForge.Application.Definitions;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes workflow definition management operations.
/// </summary>
[ApiController]
[Route("api/v1/workflows")]
public sealed class WorkflowDefinitionsController : ControllerBase
{
    private readonly IWorkflowDefinitionService _service;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public WorkflowDefinitionsController(IWorkflowDefinitionService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>
    /// Lists workflow definition versions.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.WorkflowDefinitionsRead)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        return Ok(result.Value!.Select(definition => definition.ToDto()).ToArray());
    }

    /// <summary>
    /// Gets an exact workflow definition version.
    /// </summary>
    [HttpGet("{id:guid}/{version}")]
    [Authorize(Policy = Permissions.WorkflowDefinitionsRead)]
    public async Task<IActionResult> GetAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(
            new WorkflowDefinitionId(id),
            version,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        if (result.Value is null)
        {
            return ApplicationErrorMapper.ToActionResult(
                [
                    new ApplicationError(
                        "DefinitionNotFound",
                        "The workflow definition version was not found.")
                ],
                HttpContext);
        }

        return Ok(result.Value.ToDto());
    }

    /// <summary>
    /// Creates a workflow definition version.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.WorkflowDefinitionsWrite)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request.ToDomain(), cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        var response = result.Value!.ToDto();
        return CreatedAtAction(
            nameof(GetAsync),
            new { id = response.Id, version = response.Version },
            response);
    }
}
