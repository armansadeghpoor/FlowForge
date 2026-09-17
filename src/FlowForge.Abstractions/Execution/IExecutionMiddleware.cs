using FlowForge.Abstractions.Nodes;

namespace FlowForge.Abstractions.Execution;

/// <summary>
/// Defines middleware that participates in node execution.
/// </summary>
public interface IExecutionMiddleware
{
    /// <summary>
    /// Executes middleware behavior around the next execution delegate.
    /// </summary>
    /// <param name="next">The next delegate in the execution pipeline.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The node execution result.</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        Func<CancellationToken, Task<NodeExecutionResult>> next,
        CancellationToken cancellationToken);
}
