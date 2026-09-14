using FlowForge.Abstractions.Nodes;
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
            ErrorMessage = null
        };

        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Same(output, result.Output.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SuccessfulResult_OutputCanBeNull()
    {
        var result = new NodeExecutionResult
        {
            Success = true,
            Output = null,
            ErrorMessage = null
        };

        Assert.True(result.Success);
        Assert.Null(result.Output);
    }

    [Fact]
    public void FailedResult_PreservesErrorMessage()
    {
        var result = new NodeExecutionResult
        {
            Success = false,
            Output = null,
            ErrorMessage = "Node failed."
        };

        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Equal("Node failed.", result.ErrorMessage);
    }
}
