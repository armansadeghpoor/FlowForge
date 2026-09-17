using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;

namespace FlowForge.Core.Tests.Failures;

public sealed class NodeFailureTests
{
    [Fact]
    public void NodeFailure_StoresCategoryAndMessage()
    {
        var failure = new NodeFailure
        {
            Category = NodeFailureCategory.External,
            Message = "External service failed."
        };

        Assert.Equal(NodeFailureCategory.External, failure.Category);
        Assert.Equal("External service failed.", failure.Message);
    }
}
