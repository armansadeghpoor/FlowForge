using System.Text.Json;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Auditing;

/// <summary>
/// Represents an immutable enterprise audit record for an actor and resource action.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Gets the audit entry identity.</summary>
    public required AuditEntryId Id { get; init; }

    /// <summary>Gets the provider-independent actor identity, when available.</summary>
    public required string? UserId { get; init; }

    /// <summary>Gets the tenant associated with the request, when available.</summary>
    public required TenantId? TenantId { get; init; }

    /// <summary>Gets the tenant that owns the affected resource, when available.</summary>
    public required TenantId? ResourceTenantId { get; init; }

    /// <summary>Gets the stable action name.</summary>
    public required string Action { get; init; }

    /// <summary>Gets the stable resource type.</summary>
    public required string ResourceType { get; init; }

    /// <summary>Gets the provider-independent resource identifier.</summary>
    public required string ResourceIdentifier { get; init; }

    /// <summary>Gets the action outcome.</summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>Gets the correlation identity associated with the action.</summary>
    public required ExecutionCorrelationId CorrelationId { get; init; }

    /// <summary>Gets the UTC timestamp at which the outcome was recorded.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Gets optional JSON-compatible, non-sensitive metadata.</summary>
    public required JsonElement? Metadata { get; init; }
}
