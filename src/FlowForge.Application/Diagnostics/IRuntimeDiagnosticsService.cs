using FlowForge.Application.Common;

namespace FlowForge.Application.Diagnostics;

/// <summary>
/// Defines application-level runtime diagnostics queries.
/// </summary>
public interface IRuntimeDiagnosticsService
{
    /// <summary>
    /// Gets aggregate workflow execution metrics.
    /// </summary>
    Task<ApplicationResult<ExecutionMetricsSnapshot>> GetExecutionMetricsAsync(
        CancellationToken cancellationToken);
}
