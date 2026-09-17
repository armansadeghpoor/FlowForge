using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Engine.Policies;

namespace FlowForge.Core.Tests.Policies;

public sealed class TimeoutNodeExecutionPolicyTests
{
    [Fact]
    public void Constructor_ZeroTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TimeoutNodeExecutionPolicy(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TimeoutNodeExecutionPolicy(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task ExecuteAsync_ExecutionCompletesBeforeTimeout_ReturnsResult()
    {
        var expected = Succeeded();
        var policy = new TimeoutNodeExecutionPolicy(TimeSpan.FromSeconds(1));

        var result = await policy.ExecuteAsync(
            _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutionExceedsTimeout_ReturnsCancelledFailure()
    {
        var incompleteExecution = new TaskCompletionSource<NodeExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new TimeoutNodeExecutionPolicy(TimeSpan.FromMilliseconds(20));

        var result = await policy.ExecuteAsync(
            _ => incompleteExecution.Task,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.NotNull(result.Failure);
        Assert.Equal(NodeFailureCategory.Cancelled, result.Failure.Category);
        Assert.Contains("timed out", result.Failure.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ExternalCancellation_PropagatesCancellation()
    {
        var executionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new TimeoutNodeExecutionPolicy(TimeSpan.FromMinutes(1));
        using var cancellationSource = new CancellationTokenSource();

        var executionTask = policy.ExecuteAsync(
            async token =>
            {
                executionStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Succeeded();
            },
            cancellationSource.Token);
        await executionStarted.Task;
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
    }

    private static NodeExecutionResult Succeeded() =>
        new()
        {
            Success = true,
            Output = null,
            Failure = null
        };
}
