using Dapper;
using FlowForge.Abstractions.Execution;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Coordinates exclusive workflow execution ownership in PostgreSQL.
/// </summary>
public sealed class PostgreSqlExecutionOwnershipStore : IExecutionOwnershipStore
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL execution ownership store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlExecutionOwnershipStore(PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(
        WorkflowExecutionId executionId,
        string ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        const string acquireSql =
            """
            UPDATE workflow_executions
            SET owner_id = @OwnerId
            WHERE id = @ExecutionId
              AND status = @RunningStatus
              AND owner_id IS NULL;
            """;
        const string existsSql =
            """
            SELECT EXISTS (
                SELECT 1
                FROM workflow_executions
                WHERE id = @ExecutionId
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var parameters = new
        {
            ExecutionId = executionId.Value,
            OwnerId = ownerId,
            RunningStatus = WorkflowExecutionStatus.Running.ToString()
        };
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            acquireSql,
            parameters,
            cancellationToken: cancellationToken));
        if (affectedRows == 1)
        {
            return true;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            existsSql,
            new { ExecutionId = executionId.Value },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            throw new KeyNotFoundException(
                $"Workflow execution '{executionId.Value}' was not found.");
        }

        return false;
    }
}
