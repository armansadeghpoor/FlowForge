using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Nodes.Tests.Contracts;

public sealed class NodeExecutionResultTests
{
    [Fact]
    public void SuccessfulResult_CanContainOutput()
    {
        var output = new object();
        var result = new NodeExecutionResult
        {
            Success = true,
            Output = new NodeOutput { Value = output },
            Failure = null
        };

        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Same(output, result.Output.Value);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void SuccessfulResult_OutputCanBeNull()
    {
        var result = new NodeExecutionResult
        {
            Success = true,
            Output = null,
            Failure = null
        };

        Assert.True(result.Success);
        Assert.Null(result.Output);
    }

    [Fact]
    public void FailedResult_ContainsNodeFailure()
    {
        var failure = new NodeFailure
        {
            Category = NodeFailureCategory.Execution,
            Message = "Node failed."
        };
        var result = new NodeExecutionResult
        {
            Success = false,
            Output = null,
            Failure = failure
        };

        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Same(failure, result.Failure);
    }
}
