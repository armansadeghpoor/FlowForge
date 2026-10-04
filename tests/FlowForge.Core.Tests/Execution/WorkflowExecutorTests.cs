using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Engine;
using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.History;
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

        var workflow = Workflow([node]);
        var execution = await engine.ExecuteAsync(workflow, CancellationToken.None);
        var storedExecution = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var storedNode = await stateStore.GetNodeExecutionAsync(
            execution.Id,
            execution.Nodes[0].Id,
            CancellationToken.None);
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        var context = Assert.Single(contexts);
        var nodeState = Assert.Single(execution.Nodes);
        Assert.Equal(node.Id, context.NodeDefinition.Id);
        Assert.Equal(execution.Id, context.WorkflowExecutionId);
        Assert.NotEqual(Guid.Empty, execution.CorrelationId.Value);
        Assert.Equal(execution.CorrelationId, context.CorrelationId);
        Assert.Equal(execution.CorrelationId, nodeState.CorrelationId);
        Assert.Equal(nodeState.Id, context.NodeExecutionId);
        Assert.Equal(1, context.AttemptNumber);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(workflow.Version, execution.DefinitionVersion);
        Assert.Equal(NodeExecutionStatus.Succeeded, nodeState.Status);
        Assert.NotNull(execution.StartedAt);
        Assert.NotNull(execution.CompletedAt);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, storedExecution.Status);
        Assert.Equal(workflow.Version, storedExecution.DefinitionVersion);
        Assert.Equal(execution.CompletedAt, storedExecution.CompletedAt);
        Assert.Equal(nodeState, Assert.Single(storedExecution.Nodes));
        Assert.Equal(execution.CorrelationId, storedExecution.CorrelationId);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Succeeded, storedNode.Status);
        Assert.Equal(
            [
                ExecutionHistoryEventType.WorkflowCreated,
                ExecutionHistoryEventType.WorkflowStarted,
                ExecutionHistoryEventType.NodeStarted,
                ExecutionHistoryEventType.NodeCompleted,
                ExecutionHistoryEventType.WorkflowCompleted
            ],
            history.Select(entry => entry.EventType));
        Assert.Equal(
            nodeState.Id,
            history.Single(entry => entry.EventType == ExecutionHistoryEventType.NodeStarted)
                .NodeExecutionId);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitCorrelation_PropagatesToExecutionNodeAndHistory()
    {
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var requestId = new ExecutionRequestId(Guid.NewGuid());
        var contexts = new ConcurrentQueue<NodeExecutionContext>();
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(stateStore, new FakeNodeRunner(
            "test",
            (context, _) =>
            {
                contexts.Enqueue(context);
                return Task.FromResult(Succeeded());
            }));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            new WorkflowExecutionRequest
            {
                ExecutionRequestId = requestId,
                CorrelationId = correlationId
            },
            CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(execution.Id, CancellationToken.None);
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        Assert.Equal(correlationId, execution.CorrelationId);
        Assert.Equal(correlationId, Assert.Single(contexts).CorrelationId);
        Assert.Equal(correlationId, Assert.Single(execution.Nodes).CorrelationId);
        Assert.NotNull(stored);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Equal(correlationId, Assert.Single(stored.Nodes).CorrelationId);
        Assert.NotEmpty(history);
        Assert.All(history, entry =>
        {
            Assert.Equal(
                correlationId.Value,
                entry.Metadata?.GetProperty("correlationId").GetGuid());
            Assert.Equal(
                requestId.Value,
                entry.Metadata?.GetProperty("executionRequestId").GetGuid());
        });
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
                executionOrder.Enqueue(context.NodeDefinition.Id);
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
                if (context.NodeDefinition.Id == nodeB.Id)
                {
                    nodeBStarted.TrySetResult();
                    await releaseParallelLayer.Task.WaitAsync(cancellationToken);
                }
                else if (context.NodeDefinition.Id == nodeC.Id)
                {
                    nodeCStarted.TrySetResult();
                    await releaseParallelLayer.Task.WaitAsync(cancellationToken);
                }
                else if (context.NodeDefinition.Id == nodeD.Id)
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
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        var nodeState = Assert.Single(execution.Nodes);
        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(NodeExecutionStatus.Failed, nodeState.Status);
        Assert.Contains("missing", nodeState.Failure?.Message ?? string.Empty);
        Assert.Equal(NodeFailureCategory.Configuration, nodeState.Failure?.Category);
        Assert.NotNull(execution.CompletedAt);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Failed, storedExecution.Status);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Failed, storedNode.Status);
        Assert.Equal(nodeState.Failure, storedNode.Failure);
        Assert.Equal(nodeState, Assert.Single(storedExecution.Nodes));
        Assert.Contains(
            history,
            entry => entry.EventType == ExecutionHistoryEventType.NodeFailed &&
                     entry.NodeExecutionId == nodeState.Id);
        Assert.Equal(
            ExecutionHistoryEventType.WorkflowFailed,
            history[^1].EventType);
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
                invocations.Enqueue(context.NodeDefinition.Id);
                return Task.FromResult(
                    context.NodeDefinition.Id == nodeA.Id
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
        Assert.Equal("Node failed.", nodeState.Failure?.Message);
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Failed, storedExecution.Status);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Failed, storedNode.Status);
        Assert.Equal(NodeFailureCategory.Execution, storedNode.Failure?.Category);
        Assert.Equal(nodeState.Failure, storedNode.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_RetryPolicy_FirstFailureThenSuccess_SucceedsWorkflow()
    {
        var attempts = 0;
        var contexts = new List<NodeExecutionContext>();
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(
            stateStore,
            [new RetryNodeExecutionPolicy(2, TimeSpan.Zero)],
            new FakeNodeRunner(
                "test",
                async (context, token) =>
                {
                    contexts.Add(context);
                    var running = await stateStore.GetNodeExecutionAsync(
                        context.WorkflowExecutionId, context.NodeExecutionId, token);
                    Assert.NotNull(running);
                    Assert.Equal(NodeExecutionStatus.Running, running.Status);
                    Assert.Equal(context.AttemptNumber, running.AttemptNumber);
                    Assert.Equal(context.AttemptNumber - 1, running.RetryCount);
                    return Interlocked.Increment(ref attempts) == 1
                        ? Failed("External failure.", NodeFailureCategory.External)
                        : Succeeded();
                }));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal([1, 2], contexts.Select(context => context.AttemptNumber));
        Assert.All(contexts, context =>
        {
            Assert.Equal(execution.Id, context.WorkflowExecutionId);
            Assert.Equal(Assert.Single(execution.Nodes).Id, context.NodeExecutionId);
        });
        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(NodeExecutionStatus.Succeeded, Assert.Single(execution.Nodes).Status);
        var stored = await stateStore.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var persistedNode = Assert.Single(stored.Nodes);
        Assert.Equal(Assert.Single(execution.Nodes), persistedNode);
        Assert.Equal(2, persistedNode.AttemptNumber);
        Assert.Equal(1, persistedNode.RetryCount);
        Assert.Null(persistedNode.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustedRetries_PersistsLastAttemptAndClassifiedFailure()
    {
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(stateStore,
            [new RetryNodeExecutionPolicy(3, TimeSpan.Zero)],
            new FakeNodeRunner("test", (context, _) => Task.FromResult(
                Failed($"Failure {context.AttemptNumber}.", NodeFailureCategory.External))));

        var execution = await engine.ExecuteAsync(Workflow([Node(1)]), CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(WorkflowExecutionStatus.Failed, stored.Status);
        var node = Assert.Single(stored.Nodes);
        Assert.Equal(Assert.Single(execution.Nodes), node);
        Assert.Equal(3, node.AttemptNumber);
        Assert.Equal(2, node.RetryCount);
        Assert.Equal(NodeFailureCategory.External, node.Failure?.Category);
        Assert.Equal("Failure 3.", node.Failure?.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PolicyTimeout_PreservesCancelledFailureAndFailsExecution()
    {
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(
            stateStore,
            [new TimeoutNodeExecutionPolicy(TimeSpan.FromMilliseconds(20))],
            new FakeNodeRunner(
                "test",
                async (_, cancellationToken) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return Succeeded();
                }));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            CancellationToken.None);
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        var node = Assert.Single(execution.Nodes);
        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(NodeExecutionStatus.Failed, node.Status);
        Assert.Equal(NodeFailureCategory.Cancelled, node.Failure?.Category);
        Assert.Contains("timed out", node.Failure?.Message);
        Assert.Contains(
            history,
            entry => entry.EventType == ExecutionHistoryEventType.NodeFailed);
        Assert.Equal(ExecutionHistoryEventType.WorkflowFailed, history[^1].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_PersistenceFailure_PropagatesUnchanged()
    {
        var expected = new TestPersistenceException();
        var runnerInvoked = false;
        var engine = CreateEngine(
            new FaultInjectingStateStore(createExecutionException: expected),
            new FakeNodeRunner(
                "test",
                (_, _) =>
                {
                    runnerInvoked = true;
                    return Task.FromResult(Succeeded());
                }));

        var actual = await Assert.ThrowsAsync<TestPersistenceException>(() =>
            engine.ExecuteAsync(Workflow([Node(1)]), CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.False(runnerInvoked);
    }

    [Fact]
    public async Task ExecuteAsync_NodeRunnerThrows_PersistsSafeExecutionFailure()
    {
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(
            stateStore,
            new FakeNodeRunner(
                "test",
                (_, _) => Task.FromException<NodeExecutionResult>(
                    new InvalidOperationException("secret-token-value"))));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        var node = Assert.Single(execution.Nodes);
        Assert.Equal(NodeExecutionStatus.Failed, node.Status);
        Assert.NotNull(node.CompletedAt);
        Assert.Equal(NodeFailureCategory.Execution, node.Failure?.Category);
        Assert.Equal(
            "Node execution failed because of an unexpected execution error.",
            node.Failure?.Message);
        Assert.DoesNotContain("secret-token-value", node.Failure?.Message);
        Assert.NotNull(stored);
        Assert.Equal(node, Assert.Single(stored.Nodes));
        Assert.Contains(
            history,
            entry => entry.EventType == ExecutionHistoryEventType.NodeFailed &&
                     entry.NodeExecutionId == node.Id);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareThrows_PersistsSafeExecutionFailure()
    {
        var stateStore = new InMemoryStateStore();
        var engine = CreateEngine(
            stateStore,
            [new ThrowingMiddleware(new InvalidOperationException("sensitive-detail"))],
            new FakeNodeRunner("test", (_, _) => Task.FromResult(Succeeded())));

        var execution = await engine.ExecuteAsync(
            Workflow([Node(1)]),
            CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var history = await stateStore.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        var node = Assert.Single(execution.Nodes);
        Assert.Equal(NodeExecutionStatus.Failed, node.Status);
        Assert.NotNull(node.CompletedAt);
        Assert.Equal(NodeFailureCategory.Execution, node.Failure?.Category);
        Assert.DoesNotContain("sensitive-detail", node.Failure?.Message);
        Assert.NotNull(stored);
        Assert.Equal(node, Assert.Single(stored.Nodes));
        Assert.Contains(
            history,
            entry => entry.EventType == ExecutionHistoryEventType.NodeFailed &&
                     entry.NodeExecutionId == node.Id);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_PropagatesWithoutExecutionClassification()
    {
        var executionStarted = new TaskCompletionSource<NodeExecutionContext>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateStore = new InMemoryStateStore();
        using var cancellationSource = new CancellationTokenSource();
        var engine = CreateEngine(
            stateStore,
            new FakeNodeRunner(
                "test",
                async (context, cancellationToken) =>
                {
                    executionStarted.TrySetResult(context);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return Succeeded();
                }));

        var executionTask = engine.ExecuteAsync(
            Workflow([Node(1)]),
            cancellationSource.Token);
        var context = await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
        var storedNode = await stateStore.GetNodeExecutionAsync(
            context.WorkflowExecutionId,
            context.NodeExecutionId,
            CancellationToken.None);
        Assert.NotNull(storedNode);
        Assert.NotEqual(NodeFailureCategory.Execution, storedNode.Failure?.Category);
    }

    [Fact]
    public async Task ExecuteAsync_PersistenceFailureWhileFinalizing_PropagatesUnchanged()
    {
        var expected = new TestPersistenceException();
        var contextSource = new TaskCompletionSource<NodeExecutionContext>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateStore = new FaultInjectingStateStore(
            saveNodeException: node =>
                node.Status == NodeExecutionStatus.Failed ? expected : null);
        var engine = CreateEngine(
            stateStore,
            new FakeNodeRunner(
                "test",
                (context, _) =>
                {
                    contextSource.TrySetResult(context);
                    return Task.FromException<NodeExecutionResult>(
                        new InvalidOperationException("Execution failed."));
                }));

        var actual = await Assert.ThrowsAsync<TestPersistenceException>(() =>
            engine.ExecuteAsync(Workflow([Node(1)]), CancellationToken.None));
        var context = await contextSource.Task;
        var storedNode = await stateStore.GetNodeExecutionAsync(
            context.WorkflowExecutionId,
            context.NodeExecutionId,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.NotNull(storedNode);
        Assert.Equal(NodeExecutionStatus.Running, storedNode.Status);
        Assert.Null(storedNode.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_AttemptPersistenceFailure_PropagatesUnchanged()
    {
        var expected = new TestPersistenceException();
        var attempts = 0;
        var stateStore = new FaultInjectingStateStore(
            saveNodeException: node =>
                node.Status == NodeExecutionStatus.Running && node.AttemptNumber == 2
                    ? expected
                    : null);
        var engine = CreateEngine(
            stateStore,
            [new RetryNodeExecutionPolicy(2, TimeSpan.Zero)],
            new FakeNodeRunner(
                "test",
                (_, _) => Task.FromResult(
                    ++attempts == 1
                        ? Failed("External failure.", NodeFailureCategory.External)
                        : Succeeded())));

        var actual = await Assert.ThrowsAsync<TestPersistenceException>(() =>
            engine.ExecuteAsync(Workflow([Node(1)]), CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Equal(1, attempts);
    }

    private static WorkflowEngine CreateEngine(
        IStateStore stateStore,
        params INodeRunner[] runners) =>
        CreateEngine(
            stateStore,
            Array.Empty<IExecutionMiddleware>(),
            runners);

    private static WorkflowEngine CreateEngine(
        IStateStore stateStore,
        IEnumerable<IExecutionMiddleware> middlewares,
        params INodeRunner[] runners) =>
        new(new WorkflowExecutor(
            new FakeNodeRunnerRegistry(runners),
            stateStore,
            new ExecutionPipeline(middlewares)));

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
            Id = new WorkflowDefinitionId(
                Guid.Parse("10000000-0000-0000-0000-000000000000")),
            OwnerTenantId = new TenantId(
                Guid.Parse("20000000-0000-0000-0000-000000000000")),
            Name = "Test workflow",
            Version = "test-v1",
            Description = null,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
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

    private sealed class ThrowingMiddleware(Exception exception) : IExecutionMiddleware
    {
        public Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            Func<NodeExecutionContext, CancellationToken, Task<NodeExecutionResult>> next,
            CancellationToken cancellationToken) =>
            Task.FromException<NodeExecutionResult>(exception);
    }

    private sealed class FaultInjectingStateStore(
        Exception? createExecutionException = null,
        Func<NodeExecutionState, Exception?>? saveNodeException = null) : IStateStore
    {
        private readonly InMemoryStateStore _inner = new();

        public Task CreateExecutionAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken) =>
            createExecutionException is null
                ? _inner.CreateExecutionAsync(execution, cancellationToken)
                : Task.FromException(createExecutionException);

        public Task UpdateWorkflowStatusAsync(
            WorkflowExecutionId id,
            WorkflowExecutionStatus status,
            DateTime? startedAt,
            DateTime? completedAt,
            CancellationToken cancellationToken) =>
            _inner.UpdateWorkflowStatusAsync(
                id,
                status,
                startedAt,
                completedAt,
                cancellationToken);

        public Task UpdateHeartbeatAsync(
            WorkflowExecutionId id,
            DateTime lastHeartbeatAt,
            CancellationToken cancellationToken) =>
            _inner.UpdateHeartbeatAsync(id, lastHeartbeatAt, cancellationToken);

        public Task<IReadOnlyList<WorkflowExecution>>
            FindStaleExecutionsAsync(
                DateTime threshold,
                CancellationToken cancellationToken) =>
            _inner.FindStaleExecutionsAsync(threshold, cancellationToken);

        public Task<bool> TryClaimExecutionAsync(
            WorkflowExecutionId id,
            string ownerId,
            CancellationToken cancellationToken) =>
            _inner.TryClaimExecutionAsync(id, ownerId, cancellationToken);

        public Task AppendExecutionHistoryAsync(
            ExecutionHistoryEntry entry,
            CancellationToken cancellationToken) =>
            _inner.AppendExecutionHistoryAsync(entry, cancellationToken);

        public Task<IReadOnlyList<ExecutionHistoryEntry>>
            GetExecutionHistoryAsync(
                WorkflowExecutionId executionId,
                CancellationToken cancellationToken) =>
            _inner.GetExecutionHistoryAsync(executionId, cancellationToken);

        public Task SaveNodeExecutionAsync(
            WorkflowExecutionId executionId,
            NodeExecutionState nodeExecution,
            CancellationToken cancellationToken)
        {
            var exception = saveNodeException?.Invoke(nodeExecution);
            return exception is null
                ? _inner.SaveNodeExecutionAsync(
                    executionId,
                    nodeExecution,
                    cancellationToken)
                : Task.FromException(exception);
        }

        public Task<WorkflowExecution?> GetExecutionAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken) =>
            _inner.GetExecutionAsync(id, cancellationToken);

        public Task<IReadOnlyList<WorkflowExecution>>
            FindExecutionsByCorrelationIdAsync(
                ExecutionCorrelationId correlationId,
                CancellationToken cancellationToken) =>
            _inner.FindExecutionsByCorrelationIdAsync(correlationId, cancellationToken);

        public Task<NodeExecutionState?> GetNodeExecutionAsync(
            WorkflowExecutionId executionId,
            NodeExecutionId nodeExecutionId,
            CancellationToken cancellationToken) =>
            _inner.GetNodeExecutionAsync(executionId, nodeExecutionId, cancellationToken);
    }

    private sealed class TestPersistenceException : Exception;
}
