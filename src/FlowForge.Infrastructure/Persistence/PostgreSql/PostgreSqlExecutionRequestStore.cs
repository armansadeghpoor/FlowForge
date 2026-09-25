using Dapper;
using FlowForge.Abstractions.Execution;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Atomically registers execution request identifiers in PostgreSQL.
/// </summary>
public sealed class PostgreSqlExecutionRequestStore : IExecutionRequestStore
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL execution request store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlExecutionRequestStore(PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<bool> TryRegisterAsync(
        ExecutionRequestId id,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO execution_requests (id)
            VALUES (@Id)
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id.Value },
            cancellationToken: cancellationToken));
        return affectedRows == 1;
    }
}
