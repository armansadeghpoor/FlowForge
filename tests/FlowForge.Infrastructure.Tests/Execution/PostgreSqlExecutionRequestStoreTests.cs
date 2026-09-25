using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.PostgreSql;

namespace FlowForge.Infrastructure.Tests.Execution;

public sealed class PostgreSqlExecutionRequestStoreTests : PostgreSqlIntegrationTestBase
{
    [SkippableFact]
    public async Task TryRegisterAsync_FirstRegistration_ReturnsTrue()
    {
        var store = new PostgreSqlExecutionRequestStore(CreateConnectionFactory());

        var registered = await store.TryRegisterAsync(
            new ExecutionRequestId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(registered);
    }

    [SkippableFact]
    public async Task TryRegisterAsync_DuplicateRegistration_ReturnsFalse()
    {
        var store = new PostgreSqlExecutionRequestStore(CreateConnectionFactory());
        var requestId = new ExecutionRequestId(Guid.NewGuid());

        var first = await store.TryRegisterAsync(requestId, CancellationToken.None);
        var duplicate = await store.TryRegisterAsync(requestId, CancellationToken.None);

        Assert.True(first);
        Assert.False(duplicate);
    }

    [SkippableFact]
    public async Task TryRegisterAsync_ConcurrentRegistration_AllowsOneWinner()
    {
        var store = new PostgreSqlExecutionRequestStore(CreateConnectionFactory());
        var requestId = new ExecutionRequestId(Guid.NewGuid());
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var registrations = Enumerable.Range(0, 16)
            .Select(async _ =>
            {
                await start.Task;
                return await store.TryRegisterAsync(requestId, CancellationToken.None);
            })
            .ToArray();
        start.SetResult();

        var results = await Task.WhenAll(registrations);

        Assert.Single(results, registered => registered);
    }
}
