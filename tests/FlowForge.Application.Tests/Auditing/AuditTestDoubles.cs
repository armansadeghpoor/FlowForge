using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests.Auditing;

internal sealed class RecordingAuditStore : IAuditStore
{
    public List<AuditEntry> Entries { get; } = [];

    public Task AppendAsync(
        AuditEntry entry,
        CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditEntry>>(Entries.ToArray());
}

internal sealed class StaticAuditContext(AuditContext current) : IAuditContext
{
    public AuditContext Current { get; } = current;

    public static StaticAuditContext Create(
        string? userId = "user-1",
        TenantId? tenantId = null,
        ExecutionCorrelationId? correlationId = null) =>
        new(new AuditContext
        {
            UserId = userId,
            TenantId = tenantId,
            CorrelationId = correlationId ?? new ExecutionCorrelationId(Guid.NewGuid())
        });
}

internal sealed class ThrowingAuditStore : IAuditStore
{
    public Task AppendAsync(
        AuditEntry entry,
        CancellationToken cancellationToken) =>
        Task.FromException(new IOException("audit provider detail"));

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditEntry>>([]);
}
