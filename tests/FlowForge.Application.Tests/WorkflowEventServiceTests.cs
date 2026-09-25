using System.Text.Json;
using FlowForge.Abstractions.Events;
using FlowForge.Application.Events;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests;

public sealed class WorkflowEventServiceTests
{
    [Fact]
    public async Task DispatchAsync_Success_ReturnsRuntimeResults()
    {
        IReadOnlyList<EventExecutionResult> expected =
        [
            new EventExecutionResult
            {
                EventTriggerId = new WorkflowEventTriggerId(Guid.NewGuid()),
                TriggerId = new WorkflowTriggerId(Guid.NewGuid()),
                Success = true,
                WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid())
            }
        ];
        var service = new WorkflowEventService(
            new EventRuntimeStub { Results = expected });

        var result = await service.DispatchAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task DispatchAsync_RuntimeFailure_ReturnsApplicationError()
    {
        var service = new WorkflowEventService(
            new EventRuntimeStub { Exception = new IOException("runtime detail") });

        var result = await service.DispatchAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("EventDispatchFailed", error.Code);
        Assert.DoesNotContain("runtime detail", error.Message);
    }

    private static WorkflowEventContext CreateContext() =>
        new()
        {
            EventType = "Order.Created",
            Payload = JsonSerializer.SerializeToElement(new { orderId = 42 }),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private sealed class EventRuntimeStub : IWorkflowEventRuntime
    {
        public IReadOnlyList<EventExecutionResult> Results { get; init; } = [];

        public Exception? Exception { get; init; }

        public Task<IReadOnlyList<EventExecutionResult>> DispatchAsync(
            WorkflowEventContext context,
            CancellationToken cancellationToken) =>
            Exception is null
                ? Task.FromResult(Results)
                : Task.FromException<IReadOnlyList<EventExecutionResult>>(Exception);
    }
}
