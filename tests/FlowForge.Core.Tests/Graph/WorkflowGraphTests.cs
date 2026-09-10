using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Graph;

public sealed class WorkflowGraphTests
{
    [Fact]
    public void TryCreate_ValidWorkflow_ExposesNodesDependenciesAndDependents()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var workflow = TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeA, nodeC));

        var created = WorkflowGraph.TryCreate(workflow, out var graph, out var validationResult);

        Assert.True(created);
        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);
        Assert.NotNull(graph);
        Assert.Equal([nodeA, nodeB, nodeC], graph.NodeIds);
        Assert.Empty(graph.GetDependencies(nodeA));
        Assert.Equal([nodeA], graph.GetDependencies(nodeB));
        Assert.Equal([nodeA], graph.GetDependencies(nodeC));
        Assert.Equal([nodeB, nodeC], graph.GetDependents(nodeA));
        Assert.Empty(graph.GetDependents(nodeB));
    }
}
