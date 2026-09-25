using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Engine.Triggers;

/// <summary>
/// Rejects duplicate execution requests before delegating trigger execution.
/// </summary>
public sealed class IdempotentWorkflowTriggerExecutor : IWorkflowTriggerExecutor
{
    private readonly IExecutionRequestStore _executionRequestStore;
    private readonly IWorkflowTriggerExecutor _innerExecutor;

    /// <summary>
    /// Initializes an idempotent workflow trigger executor.
    /// </summary>
    public IdempotentWorkflowTriggerExecutor(
        IExecutionRequestStore executionRequestStore,
        IWorkflowTriggerExecutor innerExecutor)
    {
        ArgumentNullException.ThrowIfNull(executionRequestStore);
        ArgumentNullException.ThrowIfNull(innerExecutor);
        _executionRequestStore = executionRequestStore;
        _innerExecutor = innerExecutor;
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionId> ExecuteAsync(
        WorkflowTriggerExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ExecutionRequestId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An execution request identifier is required.",
                nameof(context));
        }

        if (!await _executionRequestStore.TryRegisterAsync(
                context.ExecutionRequestId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Execution request '{context.ExecutionRequestId.Value}' is already registered.");
        }

        return await _innerExecutor.ExecuteAsync(context, cancellationToken);
    }
}
