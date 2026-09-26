namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Describes the outcome recorded for an audited action.
/// </summary>
public enum AuditOutcome
{
    /// <summary>The requested action completed successfully.</summary>
    Succeeded,

    /// <summary>The requested action failed.</summary>
    Failed,

    /// <summary>The requested action was denied by authorization.</summary>
    Denied
}
