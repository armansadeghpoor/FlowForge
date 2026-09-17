using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Creates PostgreSQL connections for infrastructure persistence components.
/// </summary>
public sealed class PostgreSqlConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new PostgreSQL connection factory.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public PostgreSqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates a new unopened PostgreSQL connection.
    /// </summary>
    /// <returns>A new PostgreSQL connection.</returns>
    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
