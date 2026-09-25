using System.Collections.Concurrent;
using FlowForge.Abstractions.Execution;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Execution;

/// <summary>
/// Atomically tracks execution request identifiers within the current process.
/// </summary>
public sealed class InMemoryExecutionRequestStore : IExecutionRequestStore
{
    private readonly ConcurrentDictionary<ExecutionRequestId, byte> _requests = new();

    /// <inheritdoc />
    public Task<bool> TryRegisterAsync(
        ExecutionRequestId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_requests.TryAdd(id, 0));
    }
}
