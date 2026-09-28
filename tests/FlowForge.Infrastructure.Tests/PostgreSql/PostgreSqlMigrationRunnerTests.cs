using Dapper;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Persistence.PostgreSql.Migrations;
using Npgsql;

namespace FlowForge.Infrastructure.Tests.PostgreSql;

public sealed class PostgreSqlMigrationRunnerTests : IAsyncLifetime
{
    private readonly PostgreSqlTestSchema _database =
        new(applyInitialSchema: false);

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [SkippableFact]
    public async Task RunAsync_EmptyMigrationSet_CreatesEmptyHistory()
    {
        var connectionFactory = _database.CreateConnectionFactory();
        var runner = new PostgreSqlMigrationRunner(
            connectionFactory,
            Array.Empty<PostgreSqlMigration>());

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(await ReadHistoryAsync(connectionFactory));
    }

    [SkippableFact]
    public async Task RunAsync_EmbeddedInitialMigration_CreatesSchemaAndHistory()
    {
        var connectionFactory = _database.CreateConnectionFactory();
        var runner = new PostgreSqlMigrationRunner(connectionFactory);

        await runner.RunAsync(CancellationToken.None);

        var history = Assert.Single(await ReadHistoryAsync(connectionFactory));
        Assert.Equal("001", history.Id);
        Assert.Equal("initial_schema", history.Name);
        Assert.NotEqual(default, history.AppliedAt);
        Assert.True(await TableExistsAsync(
            connectionFactory,
            "workflow_executions"));
    }

    [SkippableFact]
    public async Task RunAsync_AlreadyAppliedMigration_DoesNotExecuteAgain()
    {
        var connectionFactory = _database.CreateConnectionFactory();
        var migration = new PostgreSqlMigration(
            "001",
            "create_probe",
            "CREATE TABLE migration_probe (id UUID PRIMARY KEY);");
        var runner = new PostgreSqlMigrationRunner(connectionFactory, [migration]);

        await runner.RunAsync(CancellationToken.None);
        await runner.RunAsync(CancellationToken.None);

        var history = Assert.Single(await ReadHistoryAsync(connectionFactory));
        Assert.Equal(migration.Id, history.Id);
        Assert.True(await TableExistsAsync(connectionFactory, "migration_probe"));
    }

    [SkippableFact]
    public async Task RunAsync_MultipleMigrations_ExecutesInIdentifierOrder()
    {
        var connectionFactory = _database.CreateConnectionFactory();
        var runner = new PostgreSqlMigrationRunner(
            connectionFactory,
            [
                new PostgreSqlMigration(
                    "002",
                    "insert_probe",
                    "INSERT INTO ordered_migration_probe (value) VALUES ('second');"),
                new PostgreSqlMigration(
                    "001",
                    "create_probe",
                    "CREATE TABLE ordered_migration_probe (value TEXT NOT NULL);")
            ]);

        await runner.RunAsync(CancellationToken.None);

        var history = await ReadHistoryAsync(connectionFactory);
        Assert.Equal(["001", "002"], history.Select(entry => entry.Id));
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        Assert.Equal(
            "second",
            await connection.QuerySingleAsync<string>(
                "SELECT value FROM ordered_migration_probe;"));
    }

    [SkippableFact]
    public async Task RunAsync_FailedMigration_IsRolledBackAndNotRecorded()
    {
        var connectionFactory = _database.CreateConnectionFactory();
        var runner = new PostgreSqlMigrationRunner(
            connectionFactory,
            [
                new PostgreSqlMigration(
                    "001",
                    "create_success",
                    "CREATE TABLE successful_migration_probe (id UUID);"),
                new PostgreSqlMigration(
                    "002",
                    "create_then_fail",
                    """
                    CREATE TABLE failed_migration_probe (id UUID);
                    SELECT * FROM missing_migration_dependency;
                    """)
            ]);

        await Assert.ThrowsAsync<PostgresException>(() =>
            runner.RunAsync(CancellationToken.None));

        var history = await ReadHistoryAsync(connectionFactory);
        Assert.Equal("001", Assert.Single(history).Id);
        Assert.True(await TableExistsAsync(
            connectionFactory,
            "successful_migration_probe"));
        Assert.False(await TableExistsAsync(
            connectionFactory,
            "failed_migration_probe"));
    }

    private static async Task<IReadOnlyList<MigrationHistoryRow>> ReadHistoryAsync(
        PostgreSqlConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<MigrationHistoryRow>(
            """
            SELECT migration_id AS Id,
                   migration_name AS Name,
                   applied_at AS AppliedAt
            FROM flowforge_schema_migrations
            ORDER BY migration_id;
            """);
        return rows.ToArray();
    }

    private static async Task<bool> TableExistsAsync(
        PostgreSqlConnectionFactory connectionFactory,
        string tableName)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        return await connection.QuerySingleAsync<bool>(
            "SELECT to_regclass(@TableName) IS NOT NULL;",
            new { TableName = tableName });
    }

    private sealed class MigrationHistoryRow
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public DateTime AppliedAt { get; init; }
    }
}
