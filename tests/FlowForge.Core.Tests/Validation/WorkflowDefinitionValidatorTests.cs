using System.Text.Json;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.Validation;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Validation;

namespace FlowForge.Core.Tests.Validation;

public sealed class WorkflowDefinitionValidatorTests
{
    private readonly WorkflowDefinitionValidator _validator = new(
        new FakeNodeRunnerRegistry(
            new FakeNodeRunner(
                "configured",
                new NodeDescriptor
                {
                    Type = "configured",
                    Version = "1.0",
                    ConfigurationSchema = new Dictionary<string, NodePropertyDefinition>
                    {
                        ["requiredValue"] = new()
                        {
                            Name = "requiredValue",
                            Type = NodePropertyType.String,
                            Required = true
                        }
                    }
                })));

    [Fact]
    public void Validate_ValidWorkflow_ReturnsValidResult()
    {
        var node = Node(
            1,
            "configured",
            new Dictionary<string, JsonElement>
            {
                ["requiredValue"] = JsonSerializer.SerializeToElement("value")
            });

        var result = _validator.Validate(Workflow([node]));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_DuplicateNodeIds_ReturnsDuplicateNodeError()
    {
        var first = Node(1);
        var duplicate = first with { Type = "other" };

        var result = _validator.Validate(Workflow([first, duplicate]));

        AssertError(result, "DuplicateNodeId");
    }

    [Fact]
    public void Validate_MissingEdgeTarget_ReturnsMissingTargetError()
    {
        var node = Node(1);
        var missingNode = Node(2);

        var result = _validator.Validate(Workflow(
            [node],
            new EdgeDefinition
            {
                From = node.Id,
                To = missingNode.Id
            }));

        AssertError(result, "MissingToNode");
    }

    [Fact]
    public void Validate_GraphCycle_ReturnsCycleError()
    {
        var first = Node(1);
        var second = Node(2);

        var result = _validator.Validate(Workflow(
            [first, second],
            Edge(first, second),
            Edge(second, first)));

        AssertError(result, "CycleDetected");
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsDescriptorError()
    {
        var node = Node(1, "configured");

        var result = _validator.Validate(Workflow([node]));

        var error = Assert.Single(result.Errors);
        Assert.Equal("RequiredNodePropertyMissing", error.Code);
        Assert.Contains("requiredValue", error.Message);
    }

    [Fact]
    public void Validate_MultipleProblems_ReturnsAllValidationErrors()
    {
        var duplicate = Node(1, string.Empty);
        var missingTarget = Node(2);
        var workflow = Workflow(
            [duplicate, duplicate],
            new EdgeDefinition
            {
                From = duplicate.Id,
                To = missingTarget.Id
            }) with
        {
            Id = default,
            Name = " ",
            Version = string.Empty
        };

        var result = _validator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Equal(7, result.Errors.Count);
        Assert.Equal(2, result.Errors.Count(error => error.Code == "NodeTypeRequired"));
        Assert.Contains(result.Errors, error => error.Code == "DefinitionIdRequired");
        Assert.Contains(result.Errors, error => error.Code == "DefinitionVersionRequired");
        Assert.Contains(result.Errors, error => error.Code == "DefinitionNameRequired");
        Assert.Contains(result.Errors, error => error.Code == "DuplicateNodeId");
        Assert.Contains(result.Errors, error => error.Code == "MissingToNode");
    }

    private static void AssertError(
        WorkflowDefinitionValidationResult result,
        string code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == code);
    }

    private static NodeDefinition Node(
        int value,
        string type = "test",
        IReadOnlyDictionary<string, JsonElement>? configuration = null) =>
        new()
        {
            Id = new NodeId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")),
            Type = type,
            Configuration = configuration ?? new Dictionary<string, JsonElement>()
        };

    private static EdgeDefinition Edge(NodeDefinition from, NodeDefinition to) =>
        new()
        {
            From = from.Id,
            To = to.Id
        };

    private static WorkflowDefinition Workflow(
        IReadOnlyList<NodeDefinition> nodes,
        params EdgeDefinition[] edges) =>
        new()
        {
            Id = new WorkflowDefinitionId(
                Guid.Parse("10000000-0000-0000-0000-000000000000")),
            OwnerTenantId = new TenantId(
                Guid.Parse("20000000-0000-0000-0000-000000000000")),
            Name = "Validated workflow",
            Version = "v1",
            Description = null,
            CreatedAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            Nodes = nodes,
            Edges = edges
        };

    private sealed class FakeNodeRunnerRegistry(params INodeRunner[] runners)
        : INodeRunnerRegistry
    {
        private readonly IReadOnlyDictionary<string, INodeRunner> _runners =
            runners.ToDictionary(runner => runner.NodeType, StringComparer.Ordinal);

        public INodeRunner? Get(string nodeType) =>
            _runners.GetValueOrDefault(nodeType);
    }

    private sealed class FakeNodeRunner(
        string nodeType,
        NodeDescriptor descriptor) : INodeRunner
    {
        public string NodeType { get; } = nodeType;

        public NodeDescriptor Descriptor { get; } = descriptor;

        public Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
