using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

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

    /// <summary>Gets the workflow definition being authorized, when applicable.</summary>
    public WorkflowDefinitionId? WorkflowDefinitionId { get; init; }

    /// <summary>Gets the workflow definition version being authorized, when applicable.</summary>
    public string? DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the sharing metadata considered for non-owner visibility, when available.
    /// </summary>
    public WorkflowSharing? Sharing { get; init; }
}
