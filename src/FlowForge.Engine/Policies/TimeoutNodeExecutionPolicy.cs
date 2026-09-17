using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;

namespace FlowForge.Engine.Policies;

/// <summary>
/// Limits the amount of time allowed for a node execution.
/// </summary>
public sealed class TimeoutNodeExecutionPolicy : IExecutionMiddleware
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new timeout node execution policy.
    /// </summary>
    /// <param name="timeout">The maximum duration allowed for execution.</param>
    public TimeoutNodeExecutionPolicy(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The execution timeout must be greater than zero.");
        }

        _timeout = timeout;
    }

    /// <inheritdoc />
    public async Task<NodeExecutionResult> ExecuteAsync(
        Func<CancellationToken, Task<NodeExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            var executionTask = next(linkedSource.Token);
            return await executionTask.WaitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            return new NodeExecutionResult
            {
                Success = false,
                Output = null,
                Failure = new NodeFailure
                {
                    Category = NodeFailureCategory.Cancelled,
                    Message = $"Node execution timed out after {_timeout}."
                }
            };
        }
    }
}
