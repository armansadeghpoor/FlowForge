namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Identifies the category of a node execution failure.
/// </summary>
public enum NodeFailureCategory
{
    Unknown,
    Validation,
    Configuration,
    External,
    Execution,
    Cancelled
}
