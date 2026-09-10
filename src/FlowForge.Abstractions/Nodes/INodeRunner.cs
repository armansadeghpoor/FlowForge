namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Defines the contract for executing a specific type of workflow node.
/// </summary>
public interface INodeRunner
{
    /// <summary>
    /// Gets the node type supported by this runner.
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// Executes a workflow node.
    /// </summary>
    /// <param name="context">The node execution context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The node execution result.</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken);
}
