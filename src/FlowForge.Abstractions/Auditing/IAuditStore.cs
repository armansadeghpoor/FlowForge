using FlowForge.Core.Domain.Auditing;

namespace FlowForge.Abstractions.Auditing;

/// <summary>
/// Defines append-only enterprise audit persistence and focused query operations.
/// </summary>
public interface IAuditStore
{
    /// <summary>Appends an immutable audit entry.</summary>
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>Returns audit entries matching the supplied filters.</summary>
    Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken);
}
