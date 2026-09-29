using FlowForge.Abstractions.Health;
using FlowForge.Abstractions.Hosting;
using FlowForge.Api.Contracts;
using FlowForge.Api.Correlation;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Exposes process liveness and runtime dependency readiness.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private const string HealthyStatus = "Healthy";
    private const string UnhealthyStatus = "Unhealthy";

    private readonly ApplicationInformation _applicationInformation;
    private readonly IReadOnlyList<IReadinessCheck> _readinessChecks;

    /// <summary>
    /// Initializes a health controller without external readiness dependencies.
    /// </summary>
    /// <param name="applicationInformation">The current application metadata.</param>
    public HealthController(ApplicationInformation applicationInformation)
        : this(applicationInformation, Array.Empty<IReadinessCheck>())
    {
    }

    /// <summary>
    /// Initializes a health controller with configured readiness dependencies.
    /// </summary>
    /// <param name="applicationInformation">The current application metadata.</param>
    /// <param name="readinessChecks">The dependency checks used for readiness.</param>
    public HealthController(
        ApplicationInformation applicationInformation,
        IEnumerable<IReadinessCheck> readinessChecks)
    {
        ArgumentNullException.ThrowIfNull(applicationInformation);
        ArgumentNullException.ThrowIfNull(readinessChecks);

        _applicationInformation = applicationInformation;
        _readinessChecks = readinessChecks.ToArray();
    }

    /// <summary>
    /// Gets the legacy process-level health snapshot.
    /// </summary>
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(CreateResponse(
        HealthyStatus,
        Array.Empty<HealthDependencyResponse>()));

    /// <summary>
    /// Gets process liveness without accessing external dependencies.
    /// </summary>
    [HttpGet("live")]
    public ActionResult<HealthResponse> GetLiveness() => Ok(CreateResponse(
        HealthyStatus,
        Array.Empty<HealthDependencyResponse>()));

    /// <summary>
    /// Gets application readiness after checking configured runtime dependencies.
    /// </summary>
    [HttpGet("ready")]
    public async Task<ActionResult<HealthResponse>> GetReadinessAsync(
        CancellationToken cancellationToken)
    {
        var dependencies = new List<HealthDependencyResponse>(
            _readinessChecks.Count);
        var isReady = true;

        foreach (var readinessCheck in _readinessChecks)
        {
            var dependencyIsReady = await CheckReadinessAsync(
                readinessCheck,
                cancellationToken);
            isReady &= dependencyIsReady;
            dependencies.Add(new HealthDependencyResponse
            {
                Name = readinessCheck.Name,
                Status = dependencyIsReady ? HealthyStatus : UnhealthyStatus
            });
        }

        var response = CreateResponse(
            isReady ? HealthyStatus : UnhealthyStatus,
            dependencies.AsReadOnly());
        return isReady
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private static async Task<bool> CheckReadinessAsync(
        IReadinessCheck readinessCheck,
        CancellationToken cancellationToken)
    {
        try
        {
            return await readinessCheck.IsReadyAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private HealthResponse CreateResponse(
        string status,
        IReadOnlyList<HealthDependencyResponse> dependencies) =>
        new()
        {
            Status = status,
            Application = _applicationInformation.Name,
            Environment = _applicationInformation.Environment,
            Version = _applicationInformation.Version,
            CorrelationId = CorrelationIdMiddleware.GetCorrelationId(HttpContext),
            Dependencies = dependencies
        };
}
