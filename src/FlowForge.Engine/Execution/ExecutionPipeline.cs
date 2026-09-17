using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Executes ordered middleware around a terminal node execution delegate.
/// </summary>
public sealed class ExecutionPipeline
{
    private readonly IReadOnlyList<IExecutionMiddleware> _middlewares;

    /// <summary>
    /// Initializes a new execution pipeline.
    /// </summary>
    /// <param name="middlewares">The middleware in registration order.</param>
    public ExecutionPipeline(IEnumerable<IExecutionMiddleware> middlewares)
    {
        ArgumentNullException.ThrowIfNull(middlewares);

        var middlewareArray = middlewares.ToArray();
        if (middlewareArray.Any(middleware => middleware is null))
        {
            throw new ArgumentException(
                "The middleware collection cannot contain null elements.",
                nameof(middlewares));
        }

        _middlewares = middlewareArray;
    }

    /// <summary>
    /// Executes the configured middleware and terminal delegate.
    /// </summary>
    /// <param name="terminal">The terminal node execution delegate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The node execution result.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        Func<CancellationToken, Task<NodeExecutionResult>> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        var execution = terminal;

        for (var index = _middlewares.Count - 1; index >= 0; index--)
        {
            var middleware = _middlewares[index];
            var next = execution;
            execution = token => middleware.ExecuteAsync(next, token);
        }

        return execution(cancellationToken);
    }
}
