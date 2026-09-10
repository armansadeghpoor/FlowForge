using System.Text.Json;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Tests.Graph;

internal static class TestWorkflowFactory
{
    public static NodeId NodeId(int value) =>
        new(Guid.Parse($"00000000-0000-0000-0000-{value:D12}"));

    public static EdgeDefinition Edge(NodeId from, NodeId to) =>
        new()
        {
            From = from,
            To = to
        };

    public static WorkflowDefinition Workflow(
        IReadOnlyList<NodeId> nodeIds,
        params EdgeDefinition[] edges) =>
        new()
        {
            Id = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000000")),
            Name = "Test workflow",
            Nodes = nodeIds.Select(Node).ToArray(),
            Edges = edges
        };

    private static NodeDefinition Node(NodeId id) =>
        new()
        {
            Id = id,
            Type = "test",
            Configuration = new Dictionary<string, JsonElement>()
        };
}
