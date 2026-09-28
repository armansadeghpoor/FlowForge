using FlowForge.Abstractions.Sharing;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Infrastructure.Sharing;

/// <summary>
/// Stores immutable workflow sharing snapshots within the current process.
/// </summary>
public sealed class InMemoryWorkflowSharingStore : IWorkflowSharingStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SharingKey, WorkflowSharing> _sharing = [];
    private readonly HashSet<WorkflowSharingId> _identities = [];

    /// <inheritdoc />
    public Task SaveAsync(
        WorkflowSharing sharing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sharing);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var snapshot = Snapshot(sharing);
            var key = new SharingKey(
                snapshot.WorkflowDefinitionId,
                snapshot.DefinitionVersion);
            if (_sharing.ContainsKey(key) || !_identities.Add(snapshot.Id))
            {
                throw new InvalidOperationException(
                    "Workflow sharing identity or definition version already exists.");
            }

            _sharing.Add(key, snapshot);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowSharing?> GetAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionVersion);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var result = _sharing.TryGetValue(
                new SharingKey(workflowDefinitionId, definitionVersion),
                out var stored)
                    ? Snapshot(stored)
                    : null;

            return Task.FromResult(result);
        }
    }

    private static WorkflowSharing Snapshot(WorkflowSharing sharing) =>
        sharing with
        {
            SharedTenantIds = Array.AsReadOnly(sharing.SharedTenantIds.ToArray())
        };

    private readonly record struct SharingKey(
        WorkflowDefinitionId WorkflowDefinitionId,
        string DefinitionVersion);
}
