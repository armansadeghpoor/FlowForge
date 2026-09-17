using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;

namespace FlowForge.Engine.Policies;

/// <summary>
/// Retries eligible node execution failures with exponential backoff.
/// </summary>
public sealed class RetryNodeExecutionPolicy : INodeExecutionPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;

    /// <summary>
    /// Initializes a new retry node execution policy.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of execution attempts.</param>
    /// <param name="initialDelay">The delay before the first retry.</param>
    public RetryNodeExecutionPolicy(int maxAttempts, TimeSpan initialDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        if (initialDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialDelay),
                initialDelay,
                "The initial retry delay cannot be negative.");
        }

        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay;
    }

    /// <inheritdoc />
    public async Task<NodeExecutionResult> ExecuteAsync(
        Func<CancellationToken, Task<NodeExecutionResult>> execution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var delay = _initialDelay;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await execution(cancellationToken);
            if (result.Success || !IsRetryable(result) || attempt == _maxAttempts)
            {
                return result;
            }

            await Task.Delay(delay, cancellationToken);
            delay += delay;
        }

        throw new InvalidOperationException("The retry policy completed without an execution result.");
    }

    private static bool IsRetryable(NodeExecutionResult result) =>
        result.Failure?.Category is NodeFailureCategory.External or NodeFailureCategory.Unknown;
}
