using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;
using FlowForge.Engine.Policies;

namespace FlowForge.Core.Tests.Policies;

public sealed class RetryNodeExecutionPolicyTests
{
    [Fact]
    public void Constructor_MaxAttemptsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RetryNodeExecutionPolicy(0, TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeInitialDelay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RetryNodeExecutionPolicy(1, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task ExecuteAsync_FirstAttemptSucceeds_ReturnsWithoutRetry()
    {
        var attempts = 0;
        var expected = Succeeded();
        var policy = new RetryNodeExecutionPolicy(3, TimeSpan.Zero);

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(expected);
            },
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ExternalFailure_Retries()
    {
        var attempts = 0;
        var expected = Succeeded();
        var policy = new RetryNodeExecutionPolicy(2, TimeSpan.Zero);

        var result = await policy.ExecuteAsync(
            _ => Task.FromResult(
                ++attempts == 1
                    ? Failed(NodeFailureCategory.External, "External failure.")
                    : expected),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFailure_Retries()
    {
        var attempts = 0;
        var expected = Succeeded();
        var policy = new RetryNodeExecutionPolicy(2, TimeSpan.Zero);

        var result = await policy.ExecuteAsync(
            _ => Task.FromResult(
                ++attempts == 1
                    ? Failed(NodeFailureCategory.Unknown, "Unknown failure.")
                    : expected),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(NodeFailureCategory.Validation)]
    [InlineData(NodeFailureCategory.Configuration)]
    [InlineData(NodeFailureCategory.Execution)]
    [InlineData(NodeFailureCategory.Cancelled)]
    public async Task ExecuteAsync_NonRetryableFailure_DoesNotRetry(
        NodeFailureCategory category)
    {
        var attempts = 0;
        var expected = Failed(category, "Non-retryable failure.");
        var policy = new RetryNodeExecutionPolicy(3, TimeSpan.Zero);

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(expected);
            },
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ExternalFailures_RespectsMaximumAttempts()
    {
        var attempts = 0;
        var policy = new RetryNodeExecutionPolicy(3, TimeSpan.Zero);

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(Failed(
                    NodeFailureCategory.External,
                    $"Failure {attempts}."));
            },
            CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.False(result.Success);
        Assert.Equal("Failure 3.", result.Failure?.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringRetryDelay_PropagatesCancellation()
    {
        var attempts = 0;
        var policy = new RetryNodeExecutionPolicy(3, TimeSpan.FromHours(1));
        using var cancellationSource = new CancellationTokenSource();

        var executionTask = policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(Failed(
                    NodeFailureCategory.External,
                    "External failure."));
            },
            cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
        Assert.Equal(1, attempts);
    }

    private static NodeExecutionResult Succeeded() =>
        new()
        {
            Success = true,
            Output = null,
            Failure = null
        };

    private static NodeExecutionResult Failed(
        NodeFailureCategory category,
        string message) =>
        new()
        {
            Success = false,
            Output = null,
            Failure = new NodeFailure
            {
                Category = category,
                Message = message
            }
        };
}
