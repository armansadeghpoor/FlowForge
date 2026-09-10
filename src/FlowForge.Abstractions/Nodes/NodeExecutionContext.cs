using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Contains the inputs required to execute a workflow node.
/// </summary>
public sealed record NodeExecutionContext
{
    /// <summary>
    /// Gets the node definition being executed.
    /// </summary>
    public required NodeDefinition Node { get; init; }

    /// <summary>
    /// Gets the identifier of the containing workflow execution.
    /// </summary>
    public required WorkflowExecutionId ExecutionId { get; init; }
}
