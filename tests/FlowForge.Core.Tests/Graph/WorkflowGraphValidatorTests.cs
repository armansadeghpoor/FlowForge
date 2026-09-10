using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Graph;

public sealed class WorkflowGraphValidatorTests
{
    private readonly WorkflowGraphValidator _validator = new();

    [Fact]
    public void Validate_ZeroNodes_ReturnsEmptyWorkflowError()
    {
        var workflow = TestWorkflowFactory.Workflow([]);

        var result = _validator.Validate(workflow);

        AssertError(result, GraphValidationErrorType.EmptyWorkflow);
    }

    [Fact]
    public void Validate_DuplicateNodeIds_ReturnsDuplicateNodeError()
    {
        var node = TestWorkflowFactory.NodeId(1);
        var workflow = TestWorkflowFactory.Workflow([node, node]);

        var result = _validator.Validate(workflow);

        var error = Assert.Single(result.Errors);
        Assert.Equal(GraphValidationErrorType.DuplicateNodeId, error.ErrorType);
        Assert.Equal(node, error.NodeId);
    }

    [Fact]
    public void Validate_MissingFromNode_ReturnsMissingFromError()
    {
        var missing = TestWorkflowFactory.NodeId(1);
        var existing = TestWorkflowFactory.NodeId(2);
        var workflow = TestWorkflowFactory.Workflow(
            [existing],
            TestWorkflowFactory.Edge(missing, existing));

        var result = _validator.Validate(workflow);

        var error = Assert.Single(result.Errors);
        Assert.Equal(GraphValidationErrorType.MissingFromNode, error.ErrorType);
        Assert.Equal(missing, error.From);
        Assert.Equal(existing, error.To);
    }

    [Fact]
    public void Validate_MissingToNode_ReturnsMissingToError()
    {
        var existing = TestWorkflowFactory.NodeId(1);
        var missing = TestWorkflowFactory.NodeId(2);
        var workflow = TestWorkflowFactory.Workflow(
            [existing],
            TestWorkflowFactory.Edge(existing, missing));

        var result = _validator.Validate(workflow);

        var error = Assert.Single(result.Errors);
        Assert.Equal(GraphValidationErrorType.MissingToNode, error.ErrorType);
        Assert.Equal(existing, error.From);
        Assert.Equal(missing, error.To);
    }

    [Fact]
    public void Validate_DuplicateEdge_ReturnsDuplicateEdgeError()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var edge = TestWorkflowFactory.Edge(nodeA, nodeB);
        var workflow = TestWorkflowFactory.Workflow([nodeA, nodeB], edge, edge);

        var result = _validator.Validate(workflow);

        AssertError(result, GraphValidationErrorType.DuplicateEdge);
    }

    [Fact]
    public void Validate_SelfLoop_ReturnsSelfReferencingEdgeError()
    {
        var node = TestWorkflowFactory.NodeId(1);
        var workflow = TestWorkflowFactory.Workflow(
            [node],
            TestWorkflowFactory.Edge(node, node));

        var result = _validator.Validate(workflow);

        var error = Assert.Single(result.Errors);
        Assert.Equal(GraphValidationErrorType.SelfReferencingEdge, error.ErrorType);
        Assert.Equal(node, error.NodeId);
    }

    [Fact]
    public void Validate_MultiNodeCycle_ReturnsCycleDetectedError()
    {
        var nodeA = TestWorkflowFactory.NodeId(1);
        var nodeB = TestWorkflowFactory.NodeId(2);
        var nodeC = TestWorkflowFactory.NodeId(3);
        var workflow = TestWorkflowFactory.Workflow(
            [nodeA, nodeB, nodeC],
            TestWorkflowFactory.Edge(nodeA, nodeB),
            TestWorkflowFactory.Edge(nodeB, nodeC),
            TestWorkflowFactory.Edge(nodeC, nodeA));

        var result = _validator.Validate(workflow);

        AssertError(result, GraphValidationErrorType.CycleDetected);
    }

    private static void AssertError(
        GraphValidationResult result,
        GraphValidationErrorType expectedErrorType)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorType == expectedErrorType);
    }
}
