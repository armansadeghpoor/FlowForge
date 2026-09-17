using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Policies;

namespace FlowForge.Core.Tests.Execution;

public sealed class ExecutionPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyPipeline_ExecutesTerminalDelegate()
    {
        var expected = Succeeded();
        var pipeline = new ExecutionPipeline([]);

        var result = await pipeline.ExecuteAsync(
            _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_SingleMiddleware_ExecutesMiddlewareAndTerminal()
    {
        var steps = new List<string>();
        var middleware = RecordingMiddleware("middleware", steps);
        var pipeline = new ExecutionPipeline([middleware]);

        await pipeline.ExecuteAsync(
            _ =>
            {
                steps.Add("terminal");
                return Task.FromResult(Succeeded());
            },
            CancellationToken.None);

        Assert.Equal(["middleware-before", "terminal", "middleware-after"], steps);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMiddleware_ExecutesInRegistrationOrder()
    {
        var steps = new List<string>();
        var pipeline = new ExecutionPipeline(
        [
            RecordingMiddleware("first", steps),
            RecordingMiddleware("second", steps)
        ]);

        await pipeline.ExecuteAsync(
            _ =>
            {
                steps.Add("terminal");
                return Task.FromResult(Succeeded());
            },
            CancellationToken.None);

        Assert.Equal(
        [
            "first-before",
            "second-before",
            "terminal",
            "second-after",
            "first-after"
        ],
            steps);
    }

    [Fact]
    public async Task ExecuteAsync_RetryMiddleware_RetriesTerminalDelegate()
    {
        var attempts = 0;
        var pipeline = new ExecutionPipeline(
            [new RetryNodeExecutionPolicy(2, TimeSpan.Zero)]);

        var result = await pipeline.ExecuteAsync(
            _ => Task.FromResult(
                ++attempts == 1
                    ? Failed(NodeFailureCategory.External, "External failure.")
                    : Succeeded()),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutMiddleware_ReturnsCancelledFailure()
    {
        var incompleteExecution = new TaskCompletionSource<NodeExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = new ExecutionPipeline(
            [new TimeoutNodeExecutionPolicy(TimeSpan.FromMilliseconds(20))]);

        var result = await pipeline.ExecuteAsync(
            _ => incompleteExecution.Task,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(NodeFailureCategory.Cancelled, result.Failure?.Category);
    }

    private static IExecutionMiddleware RecordingMiddleware(
        string name,
        ICollection<string> steps) =>
        new DelegateMiddleware(async (next, cancellationToken) =>
        {
            steps.Add($"{name}-before");
            var result = await next(cancellationToken);
            steps.Add($"{name}-after");
            return result;
        });

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

    private sealed class DelegateMiddleware(
        Func<
            Func<CancellationToken, Task<NodeExecutionResult>>,
            CancellationToken,
            Task<NodeExecutionResult>> execute) : IExecutionMiddleware
    {
        public Task<NodeExecutionResult> ExecuteAsync(
            Func<CancellationToken, Task<NodeExecutionResult>> next,
            CancellationToken cancellationToken) =>
            execute(next, cancellationToken);
    }
}
