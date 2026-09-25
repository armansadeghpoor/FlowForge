using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.PostgreSql;

namespace FlowForge.Infrastructure.Tests.Execution;

public sealed class PostgreSqlExecutionOwnershipStoreTests : PostgreSqlIntegrationTestBase
{
    [SkippableFact]
    public async Task TryAcquireAsync_UnownedExecution_ReturnsTrue()
    {
        var connectionFactory = CreateConnectionFactory();
        var execution = CreateExecution();
        var stateStore = new PostgreSqlStateStore(connectionFactory);
        var ownershipStore = new PostgreSqlExecutionOwnershipStore(connectionFactory);
        await stateStore.CreateExecutionAsync(execution, CancellationToken.None);

        var acquired = await ownershipStore.TryAcquireAsync(
            execution.Id,
            "worker-one",
            CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.True(acquired);
        Assert.NotNull(stored);
        Assert.Equal("worker-one", stored.OwnerId);
    }

    [SkippableFact]
    public async Task TryAcquireAsync_AlreadyOwnedExecution_ReturnsFalse()
    {
        var connectionFactory = CreateConnectionFactory();
        var execution = CreateExecution();
        var stateStore = new PostgreSqlStateStore(connectionFactory);
        var ownershipStore = new PostgreSqlExecutionOwnershipStore(connectionFactory);
        await stateStore.CreateExecutionAsync(execution, CancellationToken.None);
        var first = await ownershipStore.TryAcquireAsync(
            execution.Id,
            "worker-one",
            CancellationToken.None);

        var duplicate = await ownershipStore.TryAcquireAsync(
            execution.Id,
            "worker-two",
            CancellationToken.None);
        var stored = await stateStore.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.True(first);
        Assert.False(duplicate);
        Assert.NotNull(stored);
        Assert.Equal("worker-one", stored.OwnerId);
    }

    [SkippableFact]
    public async Task TryAcquireAsync_MissingExecution_ThrowsKeyNotFoundException()
    {
        var ownershipStore = new PostgreSqlExecutionOwnershipStore(
            CreateConnectionFactory());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            ownershipStore.TryAcquireAsync(
                new WorkflowExecutionId(Guid.NewGuid()),
                "worker-one",
                CancellationToken.None));
    }

    private static WorkflowExecution CreateExecution() =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            CorrelationId = new ExecutionCorrelationId(Guid.NewGuid()),
            DefinitionVersion = "test-v1",
            Status = WorkflowExecutionStatus.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = []
        };
}
