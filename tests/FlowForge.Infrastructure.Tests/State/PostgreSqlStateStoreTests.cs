using FlowForge.Abstractions.State;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.State.Conformance;
using Npgsql;

namespace FlowForge.Infrastructure.Tests.State;

public sealed class PostgreSqlStateStoreTests : StateStoreConformanceTests, IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable =
        "FLOWFORGE_POSTGRES_CONNECTION_STRING";

    private readonly string? _baseConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    private readonly string _schemaName = $"flowforge_test_{Guid.NewGuid():N}";
    private string? _storeConnectionString;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return;
        }

        await using var administrationConnection = new NpgsqlConnection(_baseConnectionString);
        await administrationConnection.OpenAsync();
        await using (var createSchema = new NpgsqlCommand(
            $"CREATE SCHEMA \"{_schemaName}\";",
            administrationConnection))
        {
            await createSchema.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            SearchPath = _schemaName
        };
        _storeConnectionString = connectionStringBuilder.ConnectionString;

        var schemaScriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "PostgreSql",
            "001_initial_schema.sql");
        var schemaScript = await File.ReadAllTextAsync(schemaScriptPath);
        await using var storeConnection = new NpgsqlConnection(_storeConnectionString);
        await storeConnection.OpenAsync();
        await using var createTables = new NpgsqlCommand(schemaScript, storeConnection);
        await createTables.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var dropSchema = new NpgsqlCommand(
            $"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE;",
            connection);
        await dropSchema.ExecuteNonQueryAsync();
    }

    protected override IStateStore CreateStore()
    {
        Skip.If(
            _storeConnectionString is null,
            $"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL conformance tests.");

        return new PostgreSqlStateStore(
            new PostgreSqlConnectionFactory(_storeConnectionString!));
    }
}
