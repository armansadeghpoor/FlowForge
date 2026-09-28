using System.Reflection;
using Dapper;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql.Migrations;

/// <summary>
/// Applies embedded PostgreSQL migrations in order and records successful applications.
/// </summary>
public sealed class PostgreSqlMigrationRunner
{
    private const long AdvisoryLockKey = 1_179_406_167;

    private const string EnsureHistoryTableSql =
        """
        CREATE TABLE IF NOT EXISTS flowforge_schema_migrations
        (
            migration_id TEXT PRIMARY KEY,
            migration_name TEXT NOT NULL,
            applied_at TIMESTAMP NOT NULL
        );
        """;

    private readonly PostgreSqlConnectionFactory _connectionFactory;
    private readonly IReadOnlyList<PostgreSqlMigration> _migrations;

    /// <summary>
    /// Initializes a runner for the migrations embedded in FlowForge.Infrastructure.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlMigrationRunner(
        PostgreSqlConnectionFactory connectionFactory)
        : this(connectionFactory, LoadEmbeddedMigrations())
    {
    }

    /// <summary>
    /// Initializes a runner for an explicit migration collection.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    /// <param name="migrations">The migrations to validate, order, and execute.</param>
    public PostgreSqlMigrationRunner(
        PostgreSqlConnectionFactory connectionFactory,
        IEnumerable<PostgreSqlMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(migrations);

        _connectionFactory = connectionFactory;
        _migrations = ValidateAndOrder(migrations);
    }

    /// <summary>
    /// Applies every pending migration in identifier order.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel database work.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var lockAcquired = false;
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_lock(@LockKey);",
                new { LockKey = AdvisoryLockKey },
                cancellationToken: cancellationToken));
            lockAcquired = true;

            await connection.ExecuteAsync(new CommandDefinition(
                EnsureHistoryTableSql,
                cancellationToken: cancellationToken));

            var appliedMigrations = (await connection
                    .QueryAsync<MigrationHistoryRow>(new CommandDefinition(
                        """
                        SELECT migration_id AS Id,
                               migration_name AS Name
                        FROM flowforge_schema_migrations;
                        """,
                        cancellationToken: cancellationToken)))
                .ToDictionary(row => row.Id, row => row.Name, StringComparer.Ordinal);

            foreach (var migration in _migrations)
            {
                if (appliedMigrations.TryGetValue(migration.Id, out var appliedName))
                {
                    if (!appliedName.Equals(migration.Name, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Applied migration '{migration.Id}' is named " +
                            $"'{appliedName}', but the configured migration is named " +
                            $"'{migration.Name}'.");
                    }

                    continue;
                }

                await ApplyMigrationAsync(connection, migration, cancellationToken);
            }
        }
        finally
        {
            if (lockAcquired)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "SELECT pg_advisory_unlock(@LockKey);",
                    new { LockKey = AdvisoryLockKey }));
            }
        }
    }

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        PostgreSqlMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                migration.Sql,
                transaction: transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO flowforge_schema_migrations
                    (migration_id, migration_name, applied_at)
                VALUES
                    (@Id, @Name, CURRENT_TIMESTAMP AT TIME ZONE 'UTC');
                """,
                new { migration.Id, migration.Name },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static IReadOnlyList<PostgreSqlMigration> ValidateAndOrder(
        IEnumerable<PostgreSqlMigration> migrations)
    {
        var migrationArray = migrations.ToArray();
        foreach (var migration in migrationArray)
        {
            ArgumentNullException.ThrowIfNull(migration);
            ArgumentException.ThrowIfNullOrWhiteSpace(migration.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(migration.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(migration.Sql);
        }

        var duplicateId = migrationArray
            .GroupBy(migration => migration.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateId is not null)
        {
            throw new InvalidOperationException(
                $"Migration identifier '{duplicateId}' is duplicated.");
        }

        return Array.AsReadOnly(migrationArray
            .OrderBy(migration => migration.Id, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<PostgreSqlMigration> LoadEmbeddedMigrations()
    {
        var assembly = typeof(PostgreSqlMigrationRunner).Assembly;
        var resourcePrefix =
            $"{assembly.GetName().Name}.Persistence.PostgreSql.Scripts.";
        var migrations = assembly.GetManifestResourceNames()
            .Where(resourceName =>
                resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                resourceName.EndsWith(".sql", StringComparison.Ordinal))
            .Select(resourceName => LoadEmbeddedMigration(
                assembly,
                resourcePrefix,
                resourceName))
            .ToArray();

        if (migrations.Length == 0)
        {
            throw new InvalidOperationException(
                "No embedded PostgreSQL migration scripts were found.");
        }

        return migrations;
    }

    private static PostgreSqlMigration LoadEmbeddedMigration(
        Assembly assembly,
        string resourcePrefix,
        string resourceName)
    {
        var fileName = resourceName[resourcePrefix.Length..];
        var separatorIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        if (separatorIndex <= 0 || !fileName.EndsWith(".sql", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration resource '{resourceName}' does not follow " +
                "the '<id>_<name>.sql' convention.");
        }

        var id = fileName[..separatorIndex];
        var name = fileName[(separatorIndex + 1)..^4];
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Migration resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return new PostgreSqlMigration(id, name, reader.ReadToEnd());
    }

    private sealed class MigrationHistoryRow
    {
        public required string Id { get; init; }

        public required string Name { get; init; }
    }
}
