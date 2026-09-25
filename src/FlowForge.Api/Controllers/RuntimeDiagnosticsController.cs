using FlowForge.Abstractions.Hosting;
using FlowForge.Abstractions.Observability;
using FlowForge.Api.Contracts;
using FlowForge.Api.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes process-level operational diagnostics.
/// </summary>
[ApiController]
[Route("api/v1/diagnostics/runtime")]
public sealed class RuntimeDiagnosticsController(
    ApplicationInformation applicationInformation,
    ApplicationUptime applicationUptime,
    IMetricsCollector metrics) : ControllerBase
{
    /// <summary>
    /// Gets application metadata, uptime, and operational metrics.
    /// </summary>
    [HttpGet]
    public ActionResult<RuntimeDiagnosticsDto> Get()
    {
        var snapshot = metrics.GetSnapshot();
        return Ok(new RuntimeDiagnosticsDto
        {
            ApplicationName = applicationInformation.Name,
            ApplicationVersion = applicationInformation.Version,
            Environment = applicationInformation.Environment,
            Uptime = applicationUptime.GetUptime(),
            Metrics = new OperationalMetricsDto
            {
                Counters = snapshot.Counters.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal),
                Durations = snapshot.Durations.ToDictionary(
                    pair => pair.Key,
                    pair => new DurationMetricDto
                    {
                        Count = pair.Value.Count,
                        Total = pair.Value.Total,
                        Average = pair.Value.Average
                    },
                    StringComparer.Ordinal)
            }
        });
    }
}
