namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes how a workflow definition may be visible beyond its owner.
/// </summary>
public enum WorkflowVisibility
{
    /// <summary>The workflow remains visible only within its ownership boundary.</summary>
    Private,

    /// <summary>The workflow is visible to its owning tenant.</summary>
    TenantVisible,

    /// <summary>The workflow is visible to explicitly selected tenants.</summary>
    Shared
}
