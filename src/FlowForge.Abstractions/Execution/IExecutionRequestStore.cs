using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Execution;

/// <summary>
/// Defines atomic registration of workflow execution requests.
/// </summary>
public interface IExecutionRequestStore
{
    /// <summary>
    /// Registers an execution request when its identifier has not been seen before.
    /// </summary>
    /// <param name="id">The execution request identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> for the first registration; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    Task<bool> TryRegisterAsync(
        ExecutionRequestId id,
        CancellationToken cancellationToken);
}
