using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Auditing;

/// <summary>
/// Represents immutable actor, tenant, and correlation information for an audited operation.
/// </summary>
public sealed record AuditContext
{
    /// <summary>Gets the authenticated actor identity, when available.</summary>
    public required string? UserId { get; init; }

    /// <summary>Gets the current request tenant, when available.</summary>
    public required TenantId? TenantId { get; init; }

    /// <summary>Gets the operation correlation identity.</summary>
    public required ExecutionCorrelationId CorrelationId { get; init; }
}
