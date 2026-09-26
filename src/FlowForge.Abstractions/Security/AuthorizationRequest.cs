using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Security;

/// <summary>
/// Describes provider-independent permission and ownership requirements.
/// </summary>
public sealed record AuthorizationRequest
{
    /// <summary>Gets the permission required by the operation.</summary>
    public required string Permission { get; init; }

    /// <summary>
    /// Gets the owning tenant of the workflow resource, when ownership applies.
    /// </summary>
    public TenantId? OwnerTenantId { get; init; }
}
