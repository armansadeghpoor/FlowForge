using FlowForge.Core.Domain.Executions;

namespace FlowForge.Abstractions.Queries;

/// <summary>
/// Provides storage-agnostic read access to workflow execution snapshots.
/// </summary>
public interface IExecutionSnapshotQuery
{
    /// <summary>
    /// Lists complete workflow execution aggregates.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored execution snapshots.</returns>
    Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(
        CancellationToken cancellationToken);
}
