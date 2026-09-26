using System.Collections.ObjectModel;
using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Auditing;

/// <summary>
/// Stores immutable enterprise audit snapshots in append order within the current process.
/// </summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly Lock _gate = new();
    private readonly List<AuditEntry> _entries = [];
    private readonly HashSet<AuditEntryId> _identities = [];

    /// <inheritdoc />
    public Task AppendAsync(
        AuditEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_identities.Add(entry.Id))
            {
                throw new InvalidOperationException(
                    $"Audit entry '{entry.Id.Value}' already exists.");
            }

            _entries.Add(Snapshot(entry));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<AuditEntry> entries = new ReadOnlyCollection<AuditEntry>(
                _entries
                    .Where(entry => Matches(entry, query))
                    .Select(Snapshot)
                    .ToArray());

            return Task.FromResult(entries);
        }
    }

    private static bool Matches(AuditEntry entry, AuditQuery query) =>
        (!query.TenantId.HasValue ||
            entry.TenantId == query.TenantId ||
            entry.ResourceTenantId == query.TenantId) &&
        (!query.CorrelationId.HasValue ||
            entry.CorrelationId == query.CorrelationId) &&
        (query.ResourceType is null ||
            entry.ResourceType.Equals(query.ResourceType, StringComparison.Ordinal)) &&
        (query.ResourceIdentifier is null ||
            entry.ResourceIdentifier.Equals(
                query.ResourceIdentifier,
                StringComparison.Ordinal));

    private static AuditEntry Snapshot(AuditEntry entry) =>
        entry with
        {
            Metadata = entry.Metadata is { } metadata
                ? metadata.Clone()
                : null
        };
}
