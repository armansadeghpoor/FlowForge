using FlowForge.Abstractions.Nodes;
using FlowForge.Nodes.Registry;

namespace FlowForge.Nodes.Tests.Registry;

public sealed class NodeRunnerRegistryTests
{
    [Fact]
    public void Get_RegisteredNodeType_ReturnsRunner()
    {
        var runner = new FakeNodeRunner("test");
        var registry = new NodeRunnerRegistry([runner]);

        var resolved = registry.Get("test");

        Assert.Same(runner, resolved);
    }

    [Fact]
    public void Get_UnknownNodeType_ReturnsNull()
    {
        var registry = new NodeRunnerRegistry([new FakeNodeRunner("known")]);

        var resolved = registry.Get("unknown");

        Assert.Null(resolved);
    }

    [Fact]
    public void Constructor_MultipleDifferentNodeTypes_RegistersAllRunners()
    {
        var firstRunner = new FakeNodeRunner("first");
        var secondRunner = new FakeNodeRunner("second");
        var registry = new NodeRunnerRegistry([firstRunner, secondRunner]);

        Assert.Same(firstRunner, registry.Get("first"));
        Assert.Same(secondRunner, registry.Get("second"));
    }

    [Fact]
    public void Constructor_DuplicateNodeType_ThrowsInvalidOperationException()
    {
        var firstRunner = new FakeNodeRunner("duplicate");
        var secondRunner = new FakeNodeRunner("duplicate");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new NodeRunnerRegistry([firstRunner, secondRunner]));

        Assert.Contains("duplicate", exception.Message);
    }

    [Fact]
    public void Registry_NodeTypeComparison_UsesOrdinalSemantics()
    {
        var upperCaseRunner = new FakeNodeRunner("Email");
        var lowerCaseRunner = new FakeNodeRunner("email");
        var registry = new NodeRunnerRegistry([upperCaseRunner, lowerCaseRunner]);

        Assert.Same(upperCaseRunner, registry.Get("Email"));
        Assert.Same(lowerCaseRunner, registry.Get("email"));
        Assert.Null(registry.Get("EMAIL"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespaceNodeType_ThrowsArgumentException(string nodeType)
    {
        var runner = new FakeNodeRunner(nodeType);

        Assert.Throws<ArgumentException>(() => new NodeRunnerRegistry([runner]));
    }

    [Fact]
    public void Constructor_NullRunnerCollection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NodeRunnerRegistry(null!));
    }

    [Fact]
    public void Constructor_NullRunnerElement_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NodeRunnerRegistry([null!]));
    }

    [Fact]
    public void Get_NullNodeType_ThrowsArgumentNullException()
    {
        var registry = new NodeRunnerRegistry([]);

        Assert.Throws<ArgumentNullException>(() => registry.Get(null!));
    }

    private sealed class FakeNodeRunner(string nodeType) : INodeRunner
    {
        public string NodeType { get; } = nodeType;

        public NodeDescriptor Descriptor { get; } = new()
        {
            Type = nodeType,
            Version = "1.0",
            ConfigurationSchema = new Dictionary<string, NodePropertyDefinition>()
        };

        public Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Output = null,
                ErrorMessage = null
            });
    }
}
