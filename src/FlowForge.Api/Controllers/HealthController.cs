using FlowForge.Abstractions.Hosting;
using FlowForge.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes application-level host health without probing external dependencies.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController(ApplicationInformation applicationInformation)
    : ControllerBase
{
    /// <summary>
    /// Gets the current application-level health snapshot.
    /// </summary>
    [HttpGet]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse
        {
            Status = "Healthy",
            Environment = applicationInformation.Environment,
            Version = applicationInformation.Version
        });
}
