using FlowForge.Abstractions.Health;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using Npgsql;

namespace FlowForge.Infrastructure.Health;

/// <summary>
/// Verifies PostgreSQL availability with a lightweight connectivity query.
/// </summary>
public sealed class PostgreSqlReadinessCheck : IReadinessCheck
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL readiness check.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlReadinessCheck(
        PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public string Name => "postgresql";

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is int value && value == 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is NpgsqlException or
                TimeoutException or
                ArgumentException or
                InvalidOperationException)
        {
            return false;
        }
    }
}
