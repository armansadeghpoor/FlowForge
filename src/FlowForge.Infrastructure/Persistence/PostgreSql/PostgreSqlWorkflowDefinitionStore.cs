using System.Collections.ObjectModel;
using System.Text.Json;
using Dapper;
using FlowForge.Abstractions.Definitions;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Stores immutable workflow definition versions in PostgreSQL.
/// </summary>
public sealed class PostgreSqlWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private const string InsertSql =
        """
        INSERT INTO workflow_definitions
            (id, version, name, description, definition, created_at)
        VALUES
            (@Id, @Version, @Name, @Description, CAST(@Definition AS jsonb), @CreatedAt);
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL workflow definition store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlWorkflowDefinitionStore(
        PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertSql,
                new
                {
                    Id = definition.Id.Value,
                    definition.Version,
                    definition.Name,
                    definition.Description,
                    Definition = SerializeGraph(definition),
                    CreatedAt = ToDatabaseTimestamp(definition.CreatedAt)
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                  exception.ConstraintName == "pk_workflow_definitions")
        {
            throw new InvalidOperationException(
                $"Workflow definition '{definition.Id.Value}' version " +
                $"'{definition.Version}' already exists.",
                exception);
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition?> GetAsync(
        WorkflowDefinitionId id,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        const string sql =
            """
            SELECT id AS Id,
                   version AS Version,
                   name AS Name,
                   description AS Description,
                   definition::text AS Definition,
                   created_at AS CreatedAt
            FROM workflow_definitions
            WHERE id = @Id
              AND version = @Version;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<WorkflowDefinitionRow>(
            new CommandDefinition(
                sql,
                new { Id = id.Value, Version = version },
                cancellationToken: cancellationToken));

        return row is null ? null : MapDefinition(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT id AS Id,
                   version AS Version,
                   name AS Name,
                   description AS Description,
                   definition::text AS Definition,
                   created_at AS CreatedAt
            FROM workflow_definitions
            ORDER BY created_at, id, version;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<WorkflowDefinitionRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return Array.AsReadOnly(rows.Select(MapDefinition).ToArray());
    }

    private static string SerializeGraph(WorkflowDefinition definition) =>
        JsonSerializer.Serialize(
            new WorkflowGraphDocument
            {
                OwnerTenantId = definition.OwnerTenantId.Value,
                Nodes = definition.Nodes.Select(MapNode).ToArray(),
                Edges = definition.Edges.Select(MapEdge).ToArray()
            },
            JsonOptions);

    private static WorkflowDefinition MapDefinition(WorkflowDefinitionRow row)
    {
        var graph = JsonSerializer.Deserialize<WorkflowGraphDocument>(
                row.Definition,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Stored workflow definition JSON is invalid.");

        return new WorkflowDefinition
        {
            Id = new WorkflowDefinitionId(row.Id),
            OwnerTenantId = new TenantId(graph.OwnerTenantId),
            Name = row.Name,
            Version = row.Version,
            Description = row.Description,
            CreatedAt = FromDatabaseTimestamp(row.CreatedAt),
            Nodes = Array.AsReadOnly(graph.Nodes.Select(MapNode).ToArray()),
            Edges = Array.AsReadOnly(graph.Edges.Select(MapEdge).ToArray())
        };
    }

    private static NodeDocument MapNode(NodeDefinition node) =>
        new()
        {
            Id = node.Id.Value,
            Type = node.Type,
            Configuration = node.Configuration.ToDictionary(
                item => item.Key,
                item => item.Value.Clone(),
                StringComparer.Ordinal)
        };

    private static NodeDefinition MapNode(NodeDocument node) =>
        new()
        {
            Id = new NodeId(node.Id),
            Type = node.Type,
            Configuration = new ReadOnlyDictionary<string, JsonElement>(
                node.Configuration.ToDictionary(
                    item => item.Key,
                    item => item.Value.Clone(),
                    StringComparer.Ordinal))
        };

    private static EdgeDocument MapEdge(EdgeDefinition edge) =>
        new()
        {
            From = edge.From.Value,
            To = edge.To.Value
        };

    private static EdgeDefinition MapEdge(EdgeDocument edge) =>
        new()
        {
            From = new NodeId(edge.From),
            To = new NodeId(edge.To)
        };

    private static DateTime ToDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime FromDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed class WorkflowDefinitionRow
    {
        public Guid Id { get; init; }

        public required string Version { get; init; }

        public required string Name { get; init; }

        public string? Description { get; init; }

        public required string Definition { get; init; }

        public DateTime CreatedAt { get; init; }
    }

    private sealed record WorkflowGraphDocument
    {
        public Guid OwnerTenantId { get; init; }

        public required IReadOnlyList<NodeDocument> Nodes { get; init; }

        public required IReadOnlyList<EdgeDocument> Edges { get; init; }
    }

    private sealed record NodeDocument
    {
        public required Guid Id { get; init; }

        public required string Type { get; init; }

        public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
    }

    private sealed record EdgeDocument
    {
        public required Guid From { get; init; }

        public required Guid To { get; init; }
    }
}
