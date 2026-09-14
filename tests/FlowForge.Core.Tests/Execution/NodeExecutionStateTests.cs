using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
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
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Output = output,
            ErrorMessage = null
        };

        Assert.Same(output, state.Output);
    }
}
