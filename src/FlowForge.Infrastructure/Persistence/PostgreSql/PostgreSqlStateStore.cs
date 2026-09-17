using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Stores workflow and node execution snapshots in PostgreSQL.
/// </summary>
public sealed class PostgreSqlStateStore : IStateStore
{
    private const string InsertWorkflowSql =
        """
        INSERT INTO workflow_executions
            (id, workflow_id, status, started_at, completed_at, created_at)
        VALUES
            (@Id, @WorkflowId, @Status, @StartedAt, @CompletedAt, @CreatedAt);
        """;

    private const string InsertNodeSql =
        """
        INSERT INTO node_executions
            (id, workflow_execution_id, node_id, status, attempt_number,
             output, failure, started_at, completed_at)
        VALUES
            (@Id, @WorkflowExecutionId, @NodeId, @Status, @AttemptNumber,
             CAST(@Output AS jsonb), CAST(@Failure AS jsonb), @StartedAt, @CompletedAt);
        """;

    private const string SaveNodeSql =
        """
        INSERT INTO node_executions
            (id, workflow_execution_id, node_id, status, attempt_number,
             output, failure, started_at, completed_at)
        VALUES
            (@Id, @WorkflowExecutionId, @NodeId, @Status, @AttemptNumber,
             CAST(@Output AS jsonb), CAST(@Failure AS jsonb), @StartedAt, @CompletedAt)
        ON CONFLICT (id) DO UPDATE SET
            workflow_execution_id = EXCLUDED.workflow_execution_id,
            node_id = EXCLUDED.node_id,
            status = EXCLUDED.status,
            attempt_number = EXCLUDED.attempt_number,
            output = EXCLUDED.output,
            failure = EXCLUDED.failure,
            started_at = EXCLUDED.started_at,
            completed_at = EXCLUDED.completed_at;
        """;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL state store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlStateStore(PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task CreateExecutionAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertWorkflowSql,
                new
                {
                    Id = execution.Id.Value,
                    WorkflowId = execution.WorkflowId.Value,
                    Status = execution.Status.ToString(),
                    StartedAt = ToDatabaseTimestamp(execution.StartedAt),
                    CompletedAt = ToDatabaseTimestamp(execution.CompletedAt),
                    CreatedAt = ToDatabaseTimestamp(execution.CreatedAt)
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var nodeExecution in execution.Nodes)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    InsertNodeSql,
                    CreateNodeParameters(execution.Id, nodeExecution),
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                  exception.ConstraintName == "workflow_executions_pkey")
        {
            throw new InvalidOperationException(
                $"Workflow execution '{execution.Id.Value}' already exists.",
                exception);
        }
    }

    /// <inheritdoc />
    public async Task UpdateWorkflowStatusAsync(
        WorkflowExecutionId id,
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE workflow_executions
            SET status = @Status,
                started_at = @StartedAt,
                completed_at = @CompletedAt
            WHERE id = @Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = id.Value,
                Status = status.ToString(),
                StartedAt = ToDatabaseTimestamp(startedAt),
                CompletedAt = ToDatabaseTimestamp(completedAt)
            },
            cancellationToken: cancellationToken));

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException(
                $"Workflow execution '{id.Value}' was not found.");
        }
    }

    /// <inheritdoc />
    public async Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);

        const string findWorkflowSql =
            """
            SELECT id
            FROM workflow_executions
            WHERE id = @Id
            FOR KEY SHARE;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var storedExecutionId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            findWorkflowSql,
            new { Id = executionId.Value },
            transaction,
            cancellationToken: cancellationToken));

        if (storedExecutionId is null)
        {
            throw new KeyNotFoundException(
                $"Workflow execution '{executionId.Value}' was not found.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            SaveNodeSql,
            CreateNodeParameters(executionId, nodeExecution),
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowExecution?> GetExecutionAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken)
    {
        const string workflowSql =
            """
            SELECT id AS Id,
                   workflow_id AS WorkflowId,
                   status AS Status,
                   started_at AS StartedAt,
                   completed_at AS CompletedAt,
                   created_at AS CreatedAt
            FROM workflow_executions
            WHERE id = @Id;
            """;
        const string nodesSql =
            """
            SELECT id AS Id,
                   node_id AS NodeId,
                   status AS Status,
                   attempt_number AS AttemptNumber,
                   output::text AS Output,
                   failure::text AS Failure,
                   started_at AS StartedAt,
                   completed_at AS CompletedAt
            FROM node_executions
            WHERE workflow_execution_id = @WorkflowExecutionId
            ORDER BY started_at NULLS FIRST, id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var workflow = await connection.QuerySingleOrDefaultAsync<WorkflowExecutionRow>(
            new CommandDefinition(
                workflowSql,
                new { Id = id.Value },
                cancellationToken: cancellationToken));
        if (workflow is null)
        {
            return null;
        }

        var nodeRows = await connection.QueryAsync<NodeExecutionRow>(new CommandDefinition(
            nodesSql,
            new { WorkflowExecutionId = id.Value },
            cancellationToken: cancellationToken));
        var nodes = nodeRows.Select(MapNodeExecution).ToArray();

        return new WorkflowExecution
        {
            Id = new WorkflowExecutionId(workflow.Id),
            WorkflowId = new WorkflowId(workflow.WorkflowId),
            Status = ParseStatus<WorkflowExecutionStatus>(workflow.Status),
            CreatedAt = FromDatabaseTimestamp(workflow.CreatedAt),
            StartedAt = FromDatabaseTimestamp(workflow.StartedAt),
            CompletedAt = FromDatabaseTimestamp(workflow.CompletedAt),
            Nodes = Array.AsReadOnly(nodes)
        };
    }

    /// <inheritdoc />
    public async Task<NodeExecutionState?> GetNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionId nodeExecutionId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT id AS Id,
                   node_id AS NodeId,
                   status AS Status,
                   attempt_number AS AttemptNumber,
                   output::text AS Output,
                   failure::text AS Failure,
                   started_at AS StartedAt,
                   completed_at AS CompletedAt
            FROM node_executions
            WHERE workflow_execution_id = @WorkflowExecutionId
              AND id = @NodeExecutionId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<NodeExecutionRow>(new CommandDefinition(
            sql,
            new
            {
                WorkflowExecutionId = executionId.Value,
                NodeExecutionId = nodeExecutionId.Value
            },
            cancellationToken: cancellationToken));

        return row is null ? null : MapNodeExecution(row);
    }

    private static DynamicParameters CreateNodeParameters(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", nodeExecution.Id.Value);
        parameters.Add("WorkflowExecutionId", executionId.Value);
        parameters.Add("NodeId", nodeExecution.NodeId.Value);
        parameters.Add("Status", nodeExecution.Status.ToString());
        parameters.Add("AttemptNumber", nodeExecution.AttemptNumber);
        parameters.Add("Output", SerializeOutput(nodeExecution.Output));
        parameters.Add("Failure", SerializeFailure(nodeExecution.Failure));
        parameters.Add("StartedAt", ToDatabaseTimestamp(nodeExecution.StartedAt));
        parameters.Add("CompletedAt", ToDatabaseTimestamp(nodeExecution.CompletedAt));
        return parameters;
    }

    private static NodeExecutionState MapNodeExecution(NodeExecutionRow row) =>
        new()
        {
            Id = new NodeExecutionId(row.Id),
            NodeId = new NodeId(row.NodeId),
            Status = ParseStatus<NodeExecutionStatus>(row.Status),
            RetryCount = Math.Max(0, row.AttemptNumber - 1),
            AttemptNumber = row.AttemptNumber,
            StartedAt = FromDatabaseTimestamp(row.StartedAt),
            CompletedAt = FromDatabaseTimestamp(row.CompletedAt),
            Output = DeserializeOutput(row.Output),
            Failure = DeserializeFailure(row.Failure)
        };

    private static string? SerializeOutput(NodeOutput? output) =>
        output is null ? null : JsonSerializer.Serialize(output.Value, JsonOptions);

    private static NodeOutput? DeserializeOutput(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return new NodeOutput { Value = ConvertJsonValue(document.RootElement) };
    }

    private static object? ConvertJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.Number when element.TryGetDecimal(out var value) => value,
            JsonValueKind.Number => element.GetDouble(),
            _ => element.Clone()
        };

    private static string? SerializeFailure(NodeFailure? failure) =>
        failure is null ? null : JsonSerializer.Serialize(failure, JsonOptions);

    private static NodeFailure? DeserializeFailure(string? json) =>
        json is null
            ? null
            : JsonSerializer.Deserialize<NodeFailure>(json, JsonOptions)
                ?? throw new InvalidOperationException("Stored node failure JSON is invalid.");

    private static TStatus ParseStatus<TStatus>(string? value)
        where TStatus : struct, Enum
    {
        if (value is not null &&
            Enum.TryParse<TStatus>(value, ignoreCase: false, out var status) &&
            Enum.IsDefined(typeof(TStatus), status))
        {
            return status;
        }

        throw new InvalidOperationException(
            $"Stored status '{value}' is not a valid {typeof(TStatus).Name} value.");
    }

    private static DateTime ToDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime? ToDatabaseTimestamp(DateTime? value) =>
        value is null ? null : ToDatabaseTimestamp(value.Value);

    private static DateTime FromDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? FromDatabaseTimestamp(DateTime? value) =>
        value is null ? null : FromDatabaseTimestamp(value.Value);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class WorkflowExecutionRow
    {
        public Guid Id { get; init; }

        public Guid WorkflowId { get; init; }

        public string? Status { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? StartedAt { get; init; }

        public DateTime? CompletedAt { get; init; }
    }

    private sealed class NodeExecutionRow
    {
        public Guid Id { get; init; }

        public Guid NodeId { get; init; }

        public string? Status { get; init; }

        public int AttemptNumber { get; init; }

        public string? Output { get; init; }

        public string? Failure { get; init; }

        public DateTime? StartedAt { get; init; }

        public DateTime? CompletedAt { get; init; }
    }
}
