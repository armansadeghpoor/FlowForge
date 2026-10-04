using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

public sealed partial class PostgreSqlStateStore
{
    /// <inheritdoc />
    public async Task ExecuteLifecycleAsync(
        WorkflowExecutionId executionId,
        Func<IExecutionLifecycleTransaction, CancellationToken, Task> transition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var lifecycle = new LifecycleTransaction(executionId, connection, transaction);
        try
        {
            await transition(lifecycle, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // A broken connection can also prevent rollback. Preserve the original
                // persistence exception; disposal closes the uncommitted transaction.
            }

            throw;
        }
        finally
        {
            lifecycle.Close();
        }
    }

    private sealed class LifecycleTransaction(
        WorkflowExecutionId executionId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IExecutionLifecycleTransaction
    {
        private bool _active = true;

        public void Close() => _active = false;

        private void Check(CancellationToken token)
        {
            if (!_active)
            {
                throw new InvalidOperationException("The lifecycle transaction has ended.");
            }
            token.ThrowIfCancellationRequested();
        }

        public Task CreateExecutionAsync(WorkflowExecution execution, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            ArgumentNullException.ThrowIfNull(execution);
            if (execution.Id != executionId)
            {
                throw new ArgumentException("Execution does not belong to this transaction.", nameof(execution));
            }
            return CreateExecutionCoreAsync(connection, transaction, execution, cancellationToken);
        }

        public Task UpdateWorkflowStatusAsync(WorkflowExecutionStatus status, DateTime? startedAt,
            DateTime? completedAt, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            return UpdateWorkflowStatusCoreAsync(
                connection, transaction, executionId, status, startedAt, completedAt, cancellationToken);
        }

        public Task SaveNodeExecutionAsync(NodeExecutionState nodeExecution, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            return SaveNodeExecutionCoreAsync(connection, transaction, executionId, nodeExecution, cancellationToken);
        }

        public Task AppendExecutionHistoryAsync(ExecutionHistoryEntry entry, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.WorkflowExecutionId != executionId)
            {
                throw new ArgumentException("History does not belong to this transaction.", nameof(entry));
            }
            return AppendExecutionHistoryCoreAsync(connection, transaction, entry, cancellationToken);
        }
    }
}
