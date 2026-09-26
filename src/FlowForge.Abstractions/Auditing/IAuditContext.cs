namespace FlowForge.Abstractions.Auditing;

/// <summary>
/// Provides actor, tenant, and correlation information for the current operation.
/// </summary>
public interface IAuditContext
{
    /// <summary>Gets the current immutable audit context.</summary>
    AuditContext Current { get; }
}
