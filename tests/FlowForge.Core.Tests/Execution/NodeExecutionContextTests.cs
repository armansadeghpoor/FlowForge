namespace FlowForge.Core.Tests.Execution;

public sealed class NodeExecutionContextTests
{
    [Fact]
    public void WithNewAttempt_PreservesOriginalContextAndExecutionIdentity()
    {
        var original = TestNodeExecutionContext.Create();

        var nextAttempt = original with { AttemptNumber = 2 };

        Assert.NotSame(original, nextAttempt);
        Assert.Equal(1, original.AttemptNumber);
        Assert.Equal(2, nextAttempt.AttemptNumber);
        Assert.Equal(original.WorkflowExecutionId, nextAttempt.WorkflowExecutionId);
        Assert.Equal(original.NodeExecutionId, nextAttempt.NodeExecutionId);
        Assert.Same(original.NodeDefinition, nextAttempt.NodeDefinition);
    }
}
