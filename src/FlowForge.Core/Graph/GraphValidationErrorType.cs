namespace FlowForge.Core.Graph;

/// <summary>
/// Identifies a workflow graph validation error category.
/// </summary>
public enum GraphValidationErrorType
{
    EmptyWorkflow,
    DuplicateNodeId,
    MissingFromNode,
    MissingToNode,
    DuplicateEdge,
    SelfReferencingEdge,
    CycleDetected
}
