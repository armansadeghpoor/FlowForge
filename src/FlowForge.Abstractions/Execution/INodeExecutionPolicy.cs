using FlowForge.Abstractions.Nodes;

namespace FlowForge.Abstractions.Execution;

/// <summary>
/// Defines a policy for executing a workflow node operation.
/// </summary>
public interface INodeExecutionPolicy
{
    /// <summary>
    /// Executes a node operation according to the policy.
    /// </summary>
    /// <param name="execution">The node operation to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The node execution result.</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        Func<CancellationToken, Task<NodeExecutionResult>> execution,
        CancellationToken cancellationToken);
}
