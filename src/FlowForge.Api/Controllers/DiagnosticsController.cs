using FlowForge.Api.Contracts;
using FlowForge.Api.Errors;
using FlowForge.Application.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes read-only runtime diagnostics.
/// </summary>
[ApiController]
[Route("api/v1/diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IRuntimeDiagnosticsService _diagnosticsService;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    public DiagnosticsController(IRuntimeDiagnosticsService diagnosticsService)
    {
        ArgumentNullException.ThrowIfNull(diagnosticsService);
        _diagnosticsService = diagnosticsService;
    }

    /// <summary>
    /// Gets aggregate workflow execution metrics.
    /// </summary>
    [HttpGet("executions")]
    public async Task<IActionResult> GetExecutionMetricsAsync(
        CancellationToken cancellationToken)
    {
        var result = await _diagnosticsService.GetExecutionMetricsAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return ApplicationErrorMapper.ToActionResult(result.Errors, HttpContext);
        }

        var metrics = result.Value!;
        return Ok(new ExecutionMetricsDto
        {
            TotalExecutions = metrics.TotalExecutions,
            RunningExecutions = metrics.RunningExecutions,
            CompletedExecutions = metrics.CompletedExecutions,
            FailedExecutions = metrics.FailedExecutions,
            AverageDuration = metrics.AverageDuration,
            LastExecutionTimestamp = metrics.LastExecutionTimestamp
        });
    }
}
