using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Execution;

/// <summary>
/// Coordinates exclusive ownership of workflow executions.
/// </summary>
public interface IExecutionOwnershipStore
{
    /// <summary>
    /// Attempts to acquire ownership of a running workflow execution.
    /// </summary>
    /// <param name="executionId">The workflow execution identifier.</param>
    /// <param name="ownerId">The identifier of the prospective owner.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when ownership is acquired; otherwise,
    /// <see langword="false"/> when the execution is already owned or is not running.
    /// </returns>
    /// <exception cref="KeyNotFoundException">The execution does not exist.</exception>
    Task<bool> TryAcquireAsync(
        WorkflowExecutionId executionId,
        string ownerId,
        CancellationToken cancellationToken);
}
