using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Graph;

public sealed class TopologicalSorterTests
{
    [Fact]
    public void Sort_SingleNodeWorkflow_ReturnsOneLayer()
    {
        var node = TestWorkflowFactory.NodeId(1);
        var graph = CreateGraph(TestWorkflowFactory.Workflow([node]));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [node]);
    }

    [Fact]
    public void Sort_LinearDag_ReturnsOneNodePerLayer()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var graph = CreateGraph(TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeB, nodeC)));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [nodeA], [nodeB], [nodeC]);
    }

    [Fact]
    public void Sort_DiamondDag_GroupsIndependentNodes()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var nodeD = TestWorkflowFactory.NodeId(4);
        var graph = CreateGraph(TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC, nodeD],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeA, nodeC),
            TestWorkflowFactory.Edge(nodeB, nodeD),
            TestWorkflowFactory.Edge(nodeC, nodeD)));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [nodeA], [nodeB, nodeC], [nodeD]);
    }

    [Fact]
    public void Sort_MultipleRoots_GroupsRootNodes()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var graph = CreateGraph(TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC],
            TestWorkflowFactory.Edge(nodeA, nodeC)));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [nodeA, nodeB], [nodeC]);
    }

    [Fact]
    public void Sort_DisconnectedComponents_ReturnsValidLayers()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var nodeD = TestWorkflowFactory.NodeId(4);
        var graph = CreateGraph(TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC, nodeD],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeC, nodeD)));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [nodeA, nodeC], [nodeB, nodeD]);
    }

    [Fact]
    public void Sort_EligibleNodes_PreservesWorkflowDefinitionOrder()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var nodeD = TestWorkflowFactory.NodeId(4);
        var graph = CreateGraph(TestWorkflowFactory.Workflow(
            [nodeA, nodeC, nodeB, nodeD],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeA, nodeC),
            TestWorkflowFactory.Edge(nodeB, nodeD),
            TestWorkflowFactory.Edge(nodeC, nodeD)));

        var layers = TopologicalSorter.Sort(graph);

        AssertLayers(layers, [nodeA], [nodeC, nodeB], [nodeD]);
    }

    private static WorkflowGraph CreateGraph(WorkflowDefinition workflow)
    {
        var created = WorkflowGraph.TryCreate(workflow, out var graph, out var validationResult);

        Assert.True(
            created,
            string.Join(Environment.NewLine, validationResult.Errors.Select(error => error.Message)));
        return Assert.IsType<WorkflowGraph>(graph);
    }

    private static void AssertLayers(
        IReadOnlyList<IReadOnlyList<NodeId>> actual,
        params NodeId[][] expected)
    {
        Assert.Equal(expected.Length, actual.Count);

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }
}
