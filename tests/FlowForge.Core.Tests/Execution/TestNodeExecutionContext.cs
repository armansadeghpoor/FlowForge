using System.Text.Json;
using FlowForge.Abstractions.Execution;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Tests.Execution;

internal static class TestNodeExecutionContext
{
    public static NodeExecutionContext Create() =>
        new()
        {
            WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid()),
            NodeExecutionId = new NodeExecutionId(Guid.NewGuid()),
            NodeDefinition = new NodeDefinition
            {
                Id = new NodeId(Guid.NewGuid()),
                Type = "test",
                Configuration = new Dictionary<string, JsonElement>()
            },
            AttemptNumber = 1
        };
}
