using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Execution;

namespace FlowForge.Infrastructure.Tests.Execution;

public sealed class InMemoryExecutionRequestStoreTests
{
    [Fact]
    public async Task TryRegisterAsync_FirstRegistration_ReturnsTrue()
    {
        var store = new InMemoryExecutionRequestStore();

        var registered = await store.TryRegisterAsync(
            new ExecutionRequestId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(registered);
    }

    [Fact]
    public async Task TryRegisterAsync_DuplicateRegistration_ReturnsFalse()
    {
        var store = new InMemoryExecutionRequestStore();
        var id = new ExecutionRequestId(Guid.NewGuid());

        var first = await store.TryRegisterAsync(id, CancellationToken.None);
        var duplicate = await store.TryRegisterAsync(id, CancellationToken.None);

        Assert.True(first);
        Assert.False(duplicate);
    }

    [Fact]
    public async Task TryRegisterAsync_ConcurrentRegistration_AllowsOneWinner()
    {
        var store = new InMemoryExecutionRequestStore();
        var id = new ExecutionRequestId(Guid.NewGuid());

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => store.TryRegisterAsync(id, CancellationToken.None)));

        Assert.Single(results, result => result);
        Assert.Equal(31, results.Count(result => !result));
    }
}
