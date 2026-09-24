using System.Text.Json;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using Npgsql;

namespace FlowForge.Infrastructure.Tests.Definitions;

public sealed class PostgreSqlWorkflowDefinitionStoreTests : IAsyncLifetime
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

    [SkippableFact]
    public async Task SaveAsync_Definition_CanBeRetrievedByIdAndVersion()
    {
        var store = CreateStore();
        var definition = CreateDefinition(version: "v1");

        await store.SaveAsync(definition, CancellationToken.None);
        var retrieved = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(definition.Id, retrieved.Id);
        Assert.Equal(definition.Name, retrieved.Name);
        Assert.Equal(definition.Version, retrieved.Version);
        Assert.Equal(definition.Description, retrieved.Description);
        Assert.Equal(definition.CreatedAt, retrieved.CreatedAt);
    }

    [SkippableFact]
    public async Task SaveAsync_MultipleVersionsOfSameDefinition_Coexist()
    {
        var store = CreateStore();
        var definitionId = new WorkflowDefinitionId(Guid.NewGuid());

        await store.SaveAsync(
            CreateDefinition(definitionId, "v1"),
            CancellationToken.None);
        await store.SaveAsync(
            CreateDefinition(definitionId, "v2"),
            CancellationToken.None);
        var definitions = await store.ListAsync(CancellationToken.None);

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, definition => definition.Version == "v1");
        Assert.Contains(definitions, definition => definition.Version == "v2");
        Assert.All(definitions, definition => Assert.Equal(definitionId, definition.Id));
    }

    [SkippableFact]
    public async Task SaveAsync_DuplicateVersion_ThrowsInvalidOperationException()
    {
        var store = CreateStore();
        var definition = CreateDefinition();
        await store.SaveAsync(definition, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(definition, CancellationToken.None));
    }

    [SkippableFact]
    public async Task SaveAsync_IsolatesStoredAndReturnedSnapshots()
    {
        var configuration = new Dictionary<string, JsonElement>
        {
            ["durationMs"] = JsonSerializer.SerializeToElement(10)
        };
        var nodes = new List<NodeDefinition>
        {
            CreateNode("delay", configuration)
        };
        var definition = CreateDefinition(nodes: nodes);
        var store = CreateStore();
        await store.SaveAsync(definition, CancellationToken.None);

        configuration["durationMs"] = JsonSerializer.SerializeToElement(99);
        nodes.Clear();
        var firstRead = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);
        var secondRead = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal(
            10,
            Assert.Single(firstRead.Nodes).Configuration["durationMs"].GetInt32());
        Assert.NotSame(firstRead, secondRead);
        Assert.NotSame(firstRead.Nodes, secondRead.Nodes);
    }

    [SkippableFact]
    public async Task SaveAsync_ComplexGraph_RoundTripsThroughPostgreSql()
    {
        var firstNode = CreateNode(
            "http",
            new Dictionary<string, JsonElement>
            {
                ["url"] = JsonSerializer.SerializeToElement("https://example.test/work"),
                ["enabled"] = JsonSerializer.SerializeToElement(true)
            });
        var secondNode = CreateNode(
            "delay",
            new Dictionary<string, JsonElement>
            {
                ["durationMs"] = JsonSerializer.SerializeToElement(25)
            });
        var definition = CreateDefinition(
            nodes: [firstNode, secondNode],
            edges:
            [
                new EdgeDefinition
                {
                    From = firstNode.Id,
                    To = secondNode.Id
                }
            ]);
        var store = CreateStore();

        await store.SaveAsync(definition, CancellationToken.None);
        var retrieved = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Nodes.Count);
        Assert.Equal("https://example.test/work", retrieved.Nodes[0]
            .Configuration["url"].GetString());
        Assert.True(retrieved.Nodes[0].Configuration["enabled"].GetBoolean());
        Assert.Equal(25, retrieved.Nodes[1].Configuration["durationMs"].GetInt32());
        var edge = Assert.Single(retrieved.Edges);
        Assert.Equal(firstNode.Id, edge.From);
        Assert.Equal(secondNode.Id, edge.To);
    }

    private PostgreSqlWorkflowDefinitionStore CreateStore()
    {
        Skip.If(
            _storeConnectionString is null,
            $"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL definition tests.");

        return new PostgreSqlWorkflowDefinitionStore(
            new PostgreSqlConnectionFactory(_storeConnectionString!));
    }

    private static WorkflowDefinition CreateDefinition(
        WorkflowDefinitionId? id = null,
        string version = "v1",
        IReadOnlyList<NodeDefinition>? nodes = null,
        IReadOnlyList<EdgeDefinition>? edges = null) =>
        new()
        {
            Id = id ?? new WorkflowDefinitionId(Guid.NewGuid()),
            Name = "PostgreSQL workflow",
            Version = version,
            Description = "PostgreSQL definition test fixture.",
            CreatedAt = new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc),
            Nodes = nodes ?? Array.Empty<NodeDefinition>(),
            Edges = edges ?? Array.Empty<EdgeDefinition>()
        };

    private static NodeDefinition CreateNode(
        string type,
        IReadOnlyDictionary<string, JsonElement> configuration) =>
        new()
        {
            Id = new NodeId(Guid.NewGuid()),
            Type = type,
            Configuration = configuration
        };
}
