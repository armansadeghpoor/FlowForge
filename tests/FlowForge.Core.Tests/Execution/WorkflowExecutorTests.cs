using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Policies;
using FlowForge.Infrastructure.State;

namespace FlowForge.Core.Tests.Execution;

public sealed class WorkflowExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SingleNode_ExecutesNodeAndSucceeds()
    {
        var node = Node(1);
        var contexts = new ConcurrentQueue<NodeExecutionContext>();
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(stateStore, new FakeNodeRunner(
            "test",
            (context, _) =>
            {
                contexts.Enqueue(context);
                return Task.FromResult(Succeeded());
            }));

        var execution = await engine.ExecuteAsync(Workflow([node]), CancellationToken.None);
        var storedExecution = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var storedNode = await stateStore.GetNodeExecutionAsync(
            execution.Id,
            execution.Nodes[0].Id,
            CancellationToken.None);

        var context = Assert.Single(contexts);
        var nodeState = Assert.Single(execution.Nodes);
        Assert.Equal(node.Id, context.Node.Id);
        Assert.Equal(execution.Id, context.ExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(NodeExecutionStatus.Succeeded, nodeState.Status);
        Assert.NotNull(execution.StartedAt);
        Assert.NotNull(execution.CompletedAt);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, storedExecution.Status);
        Assert.Equal(execution.CompletedAt, storedExecution.CompletedAt);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Succeeded, storedNode.Status);
    }

    [Fact]
    public async Task ExecuteAsync_LinearDag_ExecutesInDependencyOrder()
    {
        var nodeA = Node(1);
        var nodeB = Node(2);
        var nodeC = Node(3);
        var executionOrder = new ConcurrentQueue<NodeId>();
        var engine = CreateEngine(new InMemoryStateStore(), new FakeNodeRunner(
            "test",
            (context, _) =>
            {
                executionOrder.Enqueue(context.Node.Id);
                return Task.FromResult(Succeeded());
            }));
        var workflow = Workflow(
            [nodeA, nodeB, nodeC],
            Edge(nodeA, nodeB),
            Edge(nodeB, nodeC));

        var execution = await engine.ExecuteAsync(workflow, CancellationToken.None);

        Assert.Equal([nodeA.Id, nodeB.Id, nodeC.Id], executionOrder.ToArray());
        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal([nodeA.Id, nodeB.Id, nodeC.Id], execution.Nodes.Select(state => state.NodeId));
    }

    [Fact]
    public async Task ExecuteAsync_DiamondDag_ExecutesIndependentLayerConcurrently()
    {
        var nodeA = Node(1);
        var nodeB = Node(2);
        var nodeC = Node(3);
        var nodeD = Node(4);
        var nodeBStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nodeCStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseParallelLayer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nodeDInvocationCount = 0;
        var engine = CreateEngine(new InMemoryStateStore(), new FakeNodeRunner(
            "test",
            async (context, cancellationToken) =>
            {
                if (context.Node.Id == nodeB.Id)
                {
                    nodeBStarted.TrySetResult();
                    await releaseParallelLayer.Task.WaitAsync(cancellationToken);
                }
                else if (context.Node.Id == nodeC.Id)
                {
                    nodeCStarted.TrySetResult();
                    await releaseParallelLayer.Task.WaitAsync(cancellationToken);
                }
                else if (context.Node.Id == nodeD.Id)
                {
                    Interlocked.Increment(ref nodeDInvocationCount);
                }

                return Succeeded();
            }));
        var workflow = Workflow(
            [nodeA, nodeB, nodeC, nodeD],
            Edge(nodeA, nodeB),
            Edge(nodeA, nodeC),
            Edge(nodeB, nodeD),
            Edge(nodeC, nodeD));

        var executionTask = engine.ExecuteAsync(workflow, CancellationToken.None);

        try
        {
            await Task.WhenAll(nodeBStarted.Task, nodeCStarted.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(executionTask.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref nodeDInvocationCount));
        }
        finally
        {
            releaseParallelLayer.TrySetResult();
        }

        var execution = await executionTask;

        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(1, nodeDInvocationCount);
        Assert.Equal([nodeA.Id, nodeB.Id, nodeC.Id, nodeD.Id], execution.Nodes.Select(state => state.NodeId));
    }

    [Fact]
    public async Task ExecuteAsync_MissingNodeRunner_FailsWorkflow()
    {
        var node = Node(1, "missing");
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(stateStore);

        var execution = await engine.ExecuteAsync(Workflow([node]), CancellationToken.None);
        var storedExecution = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var storedNode = await stateStore.GetNodeExecutionAsync(
            execution.Id,
            execution.Nodes[0].Id,
            CancellationToken.None);

        var nodeState = Assert.Single(execution.Nodes);
        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(NodeExecutionStatus.Failed, nodeState.Status);
        Assert.Contains("missing", nodeState.ErrorMessage ?? string.Empty);
        Assert.NotNull(execution.CompletedAt);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Failed, storedExecution.Status);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Failed, storedNode.Status);
    }

    [Fact]
    public async Task ExecuteAsync_FailedNode_DoesNotExecuteLaterLayers()
    {
        var nodeA = Node(1);
        var nodeB = Node(2);
        var invocations = new ConcurrentQueue<NodeId>();
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(stateStore, new FakeNodeRunner(
            "test",
            (context, _) =>
            {
                invocations.Enqueue(context.Node.Id);
                return Task.FromResult(
                    context.Node.Id == nodeA.Id
                        ? Failed("Node failed.")
                        : Succeeded());
            }));
        var workflow = Workflow(
            [nodeA, nodeB],
            Edge(nodeA, nodeB));

        var execution = await engine.ExecuteAsync(workflow, CancellationToken.None);
        var storedExecution = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var storedNode = await stateStore.GetNodeExecutionAsync(
            execution.Id,
            execution.Nodes[0].Id,
            CancellationToken.None);

        var nodeState = Assert.Single(execution.Nodes);
        Assert.Equal([nodeA.Id], invocations.ToArray());
        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(NodeExecutionStatus.Failed, nodeState.Status);
        Assert.Equal("Node failed.", nodeState.ErrorMessage);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Failed, storedExecution.Status);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Failed, storedNode.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RetryPolicy_FirstFailureThenSuccess_SucceedsWorkflow()
    {
        var attempts = 0;
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(
            stateStore,
            new RetryNodeExecutionPolicy(2, TimeSpan.Zero),
            new FakeNodeRunner(
                "test",
                (_, _) => Task.FromResult(
                    Interlocked.Increment(ref attempts) == 1
                        ? Failed("External failure.", NodeFailureCategory.External)
                        : Succeeded())));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(NodeExecutionStatus.Succeeded, Assert.Single(execution.Nodes).Status);
    }

    private static WorkflowEngine CreateEngine(
        InMemoryStateStore stateStore,
        params INodeRunner[] runners) =>
        CreateEngine(
            stateStore,
            new RetryNodeExecutionPolicy(1, TimeSpan.Zero),
            runners);

    private static WorkflowEngine CreateEngine(
        InMemoryStateStore stateStore,
        INodeExecutionPolicy nodeExecutionPolicy,
        params INodeRunner[] runners) =>
        new(new WorkflowExecutor(
            new FakeNodeRunnerRegistry(runners),
            stateStore,
            nodeExecutionPolicy));

    private static NodeDefinition Node(int value, string type = "test") =>
        new()
        {
            Id = new NodeId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")),
            Type = type,
            Configuration = new Dictionary<string, JsonElement>()
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
            Id = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000000")),
            Name = "Test workflow",
            Nodes = nodes,
            Edges = edges
        };

    private static NodeExecutionResult Succeeded() =>
        new()
        {
            Success = true,
            Output = null,
            Failure = null
        };

    private static NodeExecutionResult Failed(
        string message,
        NodeFailureCategory category = NodeFailureCategory.Execution) =>
        new()
        {
            Success = false,
            Output = null,
            Failure = new NodeFailure
            {
                Category = category,
                Message = message
            }
        };

    private sealed class FakeNodeRunnerRegistry(params INodeRunner[] runners) : INodeRunnerRegistry
    {
        private readonly IReadOnlyDictionary<string, INodeRunner> _runners = runners.ToDictionary(
            runner => runner.NodeType,
            StringComparer.Ordinal);

        public INodeRunner? Get(string nodeType) =>
            _runners.GetValueOrDefault(nodeType);
    }

    private sealed class FakeNodeRunner(
        string nodeType,
        Func<NodeExecutionContext, CancellationToken, Task<NodeExecutionResult>> execute) : INodeRunner
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
            execute(context, cancellationToken);
    }
}
