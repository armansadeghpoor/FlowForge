using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Sharing;

/// <summary>
/// Represents immutable visibility metadata for one workflow definition version.
/// </summary>
public sealed record WorkflowSharing
{
    /// <summary>Gets the sharing metadata identity.</summary>
    public required WorkflowSharingId Id { get; init; }

    /// <summary>Gets the associated workflow definition identity.</summary>
    public required WorkflowDefinitionId WorkflowDefinitionId { get; init; }

    /// <summary>Gets the associated immutable definition version.</summary>
    public required string DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the workflow owner. Sharing does not transfer or replace this tenant.
    /// </summary>
    public required TenantId OwnerTenantId { get; init; }

    /// <summary>Gets the workflow visibility mode.</summary>
    public required WorkflowVisibility Visibility { get; init; }

    /// <summary>Gets the tenants explicitly granted visibility.</summary>
    public required IReadOnlyList<TenantId> SharedTenantIds { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public required DateTime CreatedAt { get; init; }
}
