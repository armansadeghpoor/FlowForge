using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Core.Tests.Execution;

public sealed class NodeExecutionStateTests
{
    [Fact]
    public void Output_CanStoreNodeOutput()
    {
        var output = new NodeOutput { Value = new object() };
        var state = new NodeExecutionState
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            Status = NodeExecutionStatus.Succeeded,
            RetryCount = 0,
            AttemptNumber = 1,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Output = output,
            Failure = null
        };

        Assert.Same(output, state.Output);
    }

    [Fact]
    public void FailureAndAttemptNumber_AreStoredInImmutableState()
    {
        var failure = new NodeFailure { Category = NodeFailureCategory.External, Message = "Unavailable." };
        var state = new NodeExecutionState
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            Status = NodeExecutionStatus.Failed,
            RetryCount = 1,
            AttemptNumber = 2,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Output = null,
            Failure = failure
        };

        var nextAttempt = state with { AttemptNumber = 3, RetryCount = 2 };

        Assert.Same(failure, state.Failure);
        Assert.Equal(2, state.AttemptNumber);
        Assert.Equal(3, nextAttempt.AttemptNumber);
    }
}
