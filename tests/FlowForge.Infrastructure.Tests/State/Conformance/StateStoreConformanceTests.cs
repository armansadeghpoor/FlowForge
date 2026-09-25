using System.Text.Json;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Infrastructure.Tests.State.Conformance;

public abstract class StateStoreConformanceTests
{
    protected abstract IStateStore CreateStore();

    [SkippableFact]
    public async Task CreateExecutionAsync_StoresRetrievableExecution()
    {
        var store = CreateStore();
        var execution = CreateExecution();

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(execution.Id, retrieved.Id);
        Assert.Equal(execution.WorkflowId, retrieved.WorkflowId);
        Assert.Equal(execution.CorrelationId, retrieved.CorrelationId);
        Assert.Equal(execution.DefinitionVersion, retrieved.DefinitionVersion);
        Assert.Equal(execution.Status, retrieved.Status);
        Assert.Equal(execution.CreatedAt, retrieved.CreatedAt);
        Assert.Equal(execution.StartedAt, retrieved.StartedAt);
        Assert.Equal(execution.CompletedAt, retrieved.CompletedAt);
        Assert.Equal(execution.OwnerId, retrieved.OwnerId);
        Assert.Equal(execution.LastHeartbeatAt, retrieved.LastHeartbeatAt);
        Assert.Equal(execution.Nodes, retrieved.Nodes);
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_DuplicateId_ThrowsInvalidOperationException()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateExecutionAsync(execution with { }, CancellationToken.None));

        Assert.Contains(execution.Id.Value.ToString(), exception.Message);
    }

    [SkippableFact]
    public async Task GetExecutionAsync_IncludesSavedNodeExecutions()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var firstNode = CreateNodeExecution();
        var secondNode = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, firstNode, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, secondNode, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Nodes.Count);
        Assert.Contains(firstNode, retrieved.Nodes);
        Assert.Contains(secondNode, retrieved.Nodes);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_SameId_ReplacesStoredNodeExecution()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution();
        var replacement = nodeExecution with
        {
            Status = NodeExecutionStatus.Succeeded,
            CompletedAt = Timestamp.AddMinutes(1),
            Output = new NodeOutput { Value = "replacement output" }
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, replacement, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.Equal(replacement, retrieved);
        Assert.NotNull(aggregate);
        Assert.Equal(replacement, Assert.Single(aggregate.Nodes));
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_PreservesFailureCategoryAndMessage()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution() with
        {
            Status = NodeExecutionStatus.Failed,
            CompletedAt = Timestamp.AddMinutes(1),
            Failure = new NodeFailure
            {
                Category = NodeFailureCategory.External,
                Message = "External service unavailable."
            }
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(NodeFailureCategory.External, retrieved.Failure?.Category);
        Assert.Equal("External service unavailable.", retrieved.Failure?.Message);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_PreservesAttemptNumber()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution() with
        {
            RetryCount = 2,
            AttemptNumber = 3
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved.AttemptNumber);
        Assert.Equal(2, retrieved.RetryCount);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_MissingWorkflow_ThrowsKeyNotFoundException()
    {
        var store = CreateStore();
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var nodeExecution = CreateNodeExecution();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SaveNodeExecutionAsync(
                executionId,
                nodeExecution,
                CancellationToken.None));

        Assert.Null(await store.GetNodeExecutionAsync(
            executionId,
            nodeExecution.Id,
            CancellationToken.None));
    }

    [SkippableFact]
    public async Task GetExecutionAsync_MissingWorkflow_ReturnsNull()
    {
        var store = CreateStore();

        var retrieved = await store.GetExecutionAsync(
            new WorkflowExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [SkippableFact]
    public async Task GetNodeExecutionAsync_MissingNode_ReturnsNull()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            new NodeExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [SkippableFact]
    public async Task FindExecutionsByCorrelationIdAsync_ReturnsCompleteMatchingAggregates()
    {
        var store = CreateStore();
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var execution = CreateExecution() with
        {
            CorrelationId = correlationId,
            OwnerId = "correlated-worker"
        };
        var nodeExecution = CreateNodeExecution() with { CorrelationId = correlationId };
        var unrelated = CreateExecution() with
        {
            CorrelationId = new ExecutionCorrelationId(Guid.NewGuid())
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.CreateExecutionAsync(unrelated, CancellationToken.None);
        await store.SaveNodeExecutionAsync(
            execution.Id,
            nodeExecution,
            CancellationToken.None);

        var matches = await store.FindExecutionsByCorrelationIdAsync(
            correlationId,
            CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(execution.Id, match.Id);
        Assert.Equal(correlationId, match.CorrelationId);
        Assert.Equal("correlated-worker", match.OwnerId);
        Assert.Equal(nodeExecution, Assert.Single(match.Nodes));
    }

    [SkippableFact]
    public async Task FindExecutionsByCorrelationIdAsync_MissingCorrelation_ReturnsEmptyCollection()
    {
        var store = CreateStore();
        await store.CreateExecutionAsync(CreateExecution(), CancellationToken.None);

        var matches = await store.FindExecutionsByCorrelationIdAsync(
            new ExecutionCorrelationId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(matches);
    }

    [SkippableFact]
    public async Task UpdateWorkflowStatusAsync_ReplacesStatusAndLifecycleTimestamps()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var startedAt = Timestamp.AddMinutes(1);
        var completedAt = startedAt.AddMinutes(2);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.UpdateWorkflowStatusAsync(
            execution.Id,
            WorkflowExecutionStatus.Succeeded,
            startedAt,
            completedAt,
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, retrieved.Status);
        Assert.Equal(startedAt, retrieved.StartedAt);
        Assert.Equal(completedAt, retrieved.CompletedAt);
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_PreservesOwnerId()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { OwnerId = "worker-01" };

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("worker-01", retrieved.OwnerId);
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_PreservesLastHeartbeatAt()
    {
        var store = CreateStore();
        var heartbeatAt = Timestamp.AddMinutes(1);
        var execution = CreateExecution() with { LastHeartbeatAt = heartbeatAt };

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(heartbeatAt, retrieved.LastHeartbeatAt);
    }

    [SkippableFact]
    public async Task UpdateHeartbeatAsync_ReplacesHeartbeatAndPreservesExecutionData()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            OwnerId = "worker-01",
            LastHeartbeatAt = Timestamp
        };
        var nodeExecution = CreateNodeExecution();
        var heartbeatAt = Timestamp.AddMinutes(2);
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        await store.UpdateHeartbeatAsync(execution.Id, heartbeatAt, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(heartbeatAt, retrieved.LastHeartbeatAt);
        Assert.Equal(execution.OwnerId, retrieved.OwnerId);
        Assert.Equal(execution.Status, retrieved.Status);
        Assert.Equal(nodeExecution, Assert.Single(retrieved.Nodes));
    }

    [SkippableFact]
    public async Task UpdateHeartbeatAsync_MissingExecution_ThrowsKeyNotFoundException()
    {
        var store = CreateStore();
        var executionId = new WorkflowExecutionId(Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.UpdateHeartbeatAsync(
                executionId,
                Timestamp,
                CancellationToken.None));
    }

    [SkippableFact]
    public async Task GetExecutionAsync_AggregatePreservesRecoveryMetadata()
    {
        var store = CreateStore();
        var heartbeatAt = Timestamp.AddMinutes(3);
        var execution = CreateExecution() with
        {
            OwnerId = "worker-02",
            LastHeartbeatAt = heartbeatAt
        };
        var nodeExecution = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("worker-02", retrieved.OwnerId);
        Assert.Equal(heartbeatAt, retrieved.LastHeartbeatAt);
        Assert.Equal(nodeExecution, Assert.Single(retrieved.Nodes));
    }

    [SkippableFact]
    public async Task FindStaleExecutionsAsync_RunningExecutionWithFreshHeartbeat_IsNotStale()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            LastHeartbeatAt = Timestamp.AddMinutes(1)
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var staleExecutions = await store.FindStaleExecutionsAsync(
            Timestamp,
            CancellationToken.None);

        Assert.Empty(staleExecutions);
    }

    [SkippableFact]
    public async Task FindStaleExecutionsAsync_RunningExecutionWithOldHeartbeat_IsStale()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            LastHeartbeatAt = Timestamp.AddMinutes(-1)
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var staleExecutions = await store.FindStaleExecutionsAsync(
            Timestamp,
            CancellationToken.None);

        Assert.Equal(execution.Id, Assert.Single(staleExecutions).Id);
    }

    [SkippableFact]
    public async Task FindStaleExecutionsAsync_RunningExecutionWithNullHeartbeat_IsStale()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            LastHeartbeatAt = null
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var staleExecutions = await store.FindStaleExecutionsAsync(
            Timestamp,
            CancellationToken.None);

        Assert.Equal(execution.Id, Assert.Single(staleExecutions).Id);
    }

    [SkippableFact]
    public async Task FindStaleExecutionsAsync_CompletedExecution_IsNotStale()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Succeeded,
            CompletedAt = Timestamp,
            LastHeartbeatAt = null
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var staleExecutions = await store.FindStaleExecutionsAsync(
            Timestamp,
            CancellationToken.None);

        Assert.Empty(staleExecutions);
    }

    [SkippableFact]
    public async Task FindStaleExecutionsAsync_ReturnsCompleteAggregates()
    {
        var store = CreateStore();
        var heartbeatAt = Timestamp.AddMinutes(-1);
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            OwnerId = "worker-03",
            LastHeartbeatAt = heartbeatAt
        };
        var nodeExecution = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        var staleExecutions = await store.FindStaleExecutionsAsync(
            Timestamp,
            CancellationToken.None);

        var staleExecution = Assert.Single(staleExecutions);
        Assert.Equal(execution.Id, staleExecution.Id);
        Assert.Equal(WorkflowExecutionStatus.Running, staleExecution.Status);
        Assert.Equal("worker-03", staleExecution.OwnerId);
        Assert.Equal(heartbeatAt, staleExecution.LastHeartbeatAt);
        Assert.Equal(nodeExecution, Assert.Single(staleExecution.Nodes));
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_UnownedRunningExecution_AcquiresOwnership()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { Status = WorkflowExecutionStatus.Running };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var claimed = await store.TryClaimExecutionAsync(
            execution.Id,
            "worker-claim-01",
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.True(claimed);
        Assert.NotNull(retrieved);
        Assert.Equal("worker-claim-01", retrieved.OwnerId);
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_AlreadyOwnedExecution_ReturnsFalse()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { Status = WorkflowExecutionStatus.Running };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var firstClaim = await store.TryClaimExecutionAsync(
            execution.Id,
            "worker-existing",
            CancellationToken.None);

        var duplicateClaim = await store.TryClaimExecutionAsync(
            execution.Id,
            "worker-contender",
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.True(firstClaim);
        Assert.False(duplicateClaim);
        Assert.NotNull(retrieved);
        Assert.Equal("worker-existing", retrieved.OwnerId);
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_ConcurrentClaims_OnlyOneAcquiresOwnership()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { Status = WorkflowExecutionStatus.Running };
        var ownerIds = Enumerable.Range(1, 16)
            .Select(index => $"worker-{index:00}")
            .ToArray();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var claimTasks = ownerIds.Select(async ownerId =>
        {
            await start.Task;
            var claimed = await store.TryClaimExecutionAsync(
                execution.Id,
                ownerId,
                CancellationToken.None);
            return (OwnerId: ownerId, Claimed: claimed);
        }).ToArray();
        start.SetResult(true);

        var claims = await Task.WhenAll(claimTasks);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        var winner = Assert.Single(claims, claim => claim.Claimed);
        Assert.NotNull(retrieved);
        Assert.Equal(winner.OwnerId, retrieved.OwnerId);
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_MissingExecution_ThrowsKeyNotFoundException()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.TryClaimExecutionAsync(
                new WorkflowExecutionId(Guid.NewGuid()),
                "worker-missing",
                CancellationToken.None));
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_CompletedExecution_ReturnsFalse()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Succeeded,
            CompletedAt = Timestamp
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var claimed = await store.TryClaimExecutionAsync(
            execution.Id,
            "worker-completed",
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.False(claimed);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.OwnerId);
    }

    [SkippableFact]
    public async Task TryClaimExecutionAsync_AggregateReadPreservesClaimedOwner()
    {
        var store = CreateStore();
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            LastHeartbeatAt = Timestamp
        };
        var nodeExecution = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        await store.TryClaimExecutionAsync(
            execution.Id,
            "worker-aggregate",
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("worker-aggregate", retrieved.OwnerId);
        Assert.Equal(execution.LastHeartbeatAt, retrieved.LastHeartbeatAt);
        Assert.Equal(nodeExecution, Assert.Single(retrieved.Nodes));
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_DefinitionVersionRoundTrips()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { DefinitionVersion = "definition-2026.09" };

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("definition-2026.09", retrieved.DefinitionVersion);
    }

    [SkippableFact]
    public async Task GetExecutionAsync_AggregatePreservesDefinitionVersion()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { DefinitionVersion = "aggregate-v2" };
        var nodeExecution = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("aggregate-v2", retrieved.DefinitionVersion);
        Assert.Equal(nodeExecution, Assert.Single(retrieved.Nodes));
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_SameWorkflowSupportsDifferentDefinitionVersions()
    {
        var store = CreateStore();
        var workflowId = new WorkflowId(Guid.NewGuid());
        var firstExecution = CreateExecution() with
        {
            WorkflowId = workflowId,
            DefinitionVersion = "v1"
        };
        var secondExecution = CreateExecution() with
        {
            WorkflowId = workflowId,
            DefinitionVersion = "v2"
        };
        await store.CreateExecutionAsync(firstExecution, CancellationToken.None);
        await store.CreateExecutionAsync(secondExecution, CancellationToken.None);

        var retrievedFirst = await store.GetExecutionAsync(
            firstExecution.Id,
            CancellationToken.None);
        var retrievedSecond = await store.GetExecutionAsync(
            secondExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrievedFirst);
        Assert.NotNull(retrievedSecond);
        Assert.Equal(workflowId, retrievedFirst.WorkflowId);
        Assert.Equal(workflowId, retrievedSecond.WorkflowId);
        Assert.Equal("v1", retrievedFirst.DefinitionVersion);
        Assert.Equal("v2", retrievedSecond.DefinitionVersion);
    }

    [SkippableFact]
    public async Task AppendExecutionHistoryAsync_AppendsAndReadsHistory()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var entry = CreateHistoryEntry(
            execution,
            ExecutionHistoryEventType.WorkflowCreated,
            Timestamp);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.AppendExecutionHistoryAsync(entry, CancellationToken.None);
        var history = await store.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        Assert.Equal(entry, Assert.Single(history));
    }

    [SkippableFact]
    public async Task GetExecutionHistoryAsync_OrdersByTimestampThenAppendOrder()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var completed = CreateHistoryEntry(
            execution,
            ExecutionHistoryEventType.WorkflowCompleted,
            Timestamp.AddMinutes(1));
        var created = CreateHistoryEntry(
            execution,
            ExecutionHistoryEventType.WorkflowCreated,
            Timestamp);
        var started = CreateHistoryEntry(
            execution,
            ExecutionHistoryEventType.WorkflowStarted,
            Timestamp);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.AppendExecutionHistoryAsync(completed, CancellationToken.None);
        await store.AppendExecutionHistoryAsync(created, CancellationToken.None);
        await store.AppendExecutionHistoryAsync(started, CancellationToken.None);
        var history = await store.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        Assert.Equal(
            [created.Id, started.Id, completed.Id],
            history.Select(entry => entry.Id));
    }

    [SkippableFact]
    public async Task GetExecutionHistoryAsync_IsolatesExecutions()
    {
        var store = CreateStore();
        var firstExecution = CreateExecution();
        var secondExecution = CreateExecution();
        var firstEntry = CreateHistoryEntry(
            firstExecution,
            ExecutionHistoryEventType.WorkflowCreated,
            Timestamp);
        var secondEntry = CreateHistoryEntry(
            secondExecution,
            ExecutionHistoryEventType.WorkflowCreated,
            Timestamp);
        await store.CreateExecutionAsync(firstExecution, CancellationToken.None);
        await store.CreateExecutionAsync(secondExecution, CancellationToken.None);
        await store.AppendExecutionHistoryAsync(firstEntry, CancellationToken.None);
        await store.AppendExecutionHistoryAsync(secondEntry, CancellationToken.None);

        var firstHistory = await store.GetExecutionHistoryAsync(
            firstExecution.Id,
            CancellationToken.None);
        var secondHistory = await store.GetExecutionHistoryAsync(
            secondExecution.Id,
            CancellationToken.None);

        Assert.Equal(firstEntry, Assert.Single(firstHistory));
        Assert.Equal(secondEntry, Assert.Single(secondHistory));
    }

    [SkippableFact]
    public async Task GetExecutionHistoryAsync_NoHistory_ReturnsEmptyCollection()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var history = await store.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        Assert.Empty(history);
    }

    [SkippableFact]
    public async Task AppendExecutionHistoryAsync_MetadataRoundTrips()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecutionId = new NodeExecutionId(Guid.NewGuid());
        var metadata = JsonSerializer.SerializeToElement(new
        {
            attemptNumber = 2,
            detail = "audit detail"
        });
        var entry = CreateHistoryEntry(
            execution,
            ExecutionHistoryEventType.NodeFailed,
            Timestamp,
            nodeExecutionId,
            metadata);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.AppendExecutionHistoryAsync(entry, CancellationToken.None);
        var history = await store.GetExecutionHistoryAsync(
            execution.Id,
            CancellationToken.None);

        var retrieved = Assert.Single(history);
        Assert.Equal(entry.Id, retrieved.Id);
        Assert.Equal(nodeExecutionId, retrieved.NodeExecutionId);
        Assert.Equal(ExecutionHistoryEventType.NodeFailed, retrieved.EventType);
        Assert.Equal(2, retrieved.Metadata?.GetProperty("attemptNumber").GetInt32());
        Assert.Equal(
            "audit detail",
            retrieved.Metadata?.GetProperty("detail").GetString());
    }

    protected static WorkflowExecution CreateExecution() =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            CorrelationId = TestCorrelationId,
            DefinitionVersion = "test-v1",
            Status = WorkflowExecutionStatus.Pending,
            CreatedAt = Timestamp,
            StartedAt = null,
            CompletedAt = null,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };

    protected static NodeExecutionState CreateNodeExecution() =>
        new()
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            CorrelationId = TestCorrelationId,
            Status = NodeExecutionStatus.Running,
            RetryCount = 0,
            AttemptNumber = 1,
            StartedAt = Timestamp,
            CompletedAt = null,
            Output = new NodeOutput { Value = "node output" },
            Failure = null
        };

    private static ExecutionHistoryEntry CreateHistoryEntry(
        WorkflowExecution execution,
        ExecutionHistoryEventType eventType,
        DateTime timestamp,
        NodeExecutionId? nodeExecutionId = null,
        JsonElement? metadata = null) =>
        new()
        {
            Id = new ExecutionHistoryId(Guid.NewGuid()),
            WorkflowExecutionId = execution.Id,
            NodeExecutionId = nodeExecutionId,
            EventType = eventType,
            Timestamp = timestamp,
            Metadata = metadata
        };

    private static DateTime Timestamp { get; } =
        new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static ExecutionCorrelationId TestCorrelationId { get; } =
        new(Guid.Parse("20000000-0000-0000-0000-000000000000"));
}
