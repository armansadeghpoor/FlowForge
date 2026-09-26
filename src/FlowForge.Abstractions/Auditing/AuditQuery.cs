using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Auditing;

/// <summary>
/// Describes the optional filters for an enterprise audit query.
/// </summary>
public sealed record AuditQuery
{
    /// <summary>
    /// Gets the tenant filter. Entries associated with the request or resource tenant match.
    /// </summary>
    public TenantId? TenantId { get; init; }

    /// <summary>Gets the correlation identity filter.</summary>
    public ExecutionCorrelationId? CorrelationId { get; init; }

    /// <summary>Gets the resource type filter.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Gets the resource identifier filter.</summary>
    public string? ResourceIdentifier { get; init; }
}
