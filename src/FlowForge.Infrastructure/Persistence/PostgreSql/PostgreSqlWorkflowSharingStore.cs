using Dapper;
using FlowForge.Abstractions.Sharing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Stores immutable workflow sharing metadata in PostgreSQL.
/// </summary>
public sealed class PostgreSqlWorkflowSharingStore : IWorkflowSharingStore
{
    private const string InsertSql =
        """
        INSERT INTO workflow_sharing
            (id, workflow_definition_id, definition_version, owner_tenant_id,
             visibility, shared_tenant_ids, created_at)
        VALUES
            (@Id, @WorkflowDefinitionId, @DefinitionVersion, @OwnerTenantId,
             @Visibility, @SharedTenantIds, @CreatedAt);
        """;

    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL workflow sharing store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlWorkflowSharingStore(
        PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        WorkflowSharing sharing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sharing);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertSql,
                new
                {
                    Id = sharing.Id.Value,
                    WorkflowDefinitionId = sharing.WorkflowDefinitionId.Value,
                    sharing.DefinitionVersion,
                    OwnerTenantId = sharing.OwnerTenantId.Value,
                    Visibility = sharing.Visibility.ToString(),
                    SharedTenantIds = sharing.SharedTenantIds
                        .Select(tenantId => tenantId.Value)
                        .ToArray(),
                    CreatedAt = ToDatabaseTimestamp(sharing.CreatedAt)
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                  (exception.ConstraintName == "workflow_sharing_pkey" ||
                   exception.ConstraintName ==
                       "uq_workflow_sharing_definition_version"))
        {
            throw new InvalidOperationException(
                "Workflow sharing identity or definition version already exists.",
                exception);
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowSharing?> GetAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionVersion);

        const string sql =
            """
            SELECT id AS Id,
                   workflow_definition_id AS WorkflowDefinitionId,
                   definition_version AS DefinitionVersion,
                   owner_tenant_id AS OwnerTenantId,
                   visibility AS Visibility,
                   shared_tenant_ids AS SharedTenantIds,
                   created_at AS CreatedAt
            FROM workflow_sharing
            WHERE workflow_definition_id = @WorkflowDefinitionId
              AND definition_version = @DefinitionVersion;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<WorkflowSharingRow>(
            new CommandDefinition(
                sql,
                new
                {
                    WorkflowDefinitionId = workflowDefinitionId.Value,
                    DefinitionVersion = definitionVersion
                },
                cancellationToken: cancellationToken));

        return row is null ? null : MapSharing(row);
    }

    private static WorkflowSharing MapSharing(WorkflowSharingRow row) =>
        new()
        {
            Id = new WorkflowSharingId(row.Id),
            WorkflowDefinitionId = new WorkflowDefinitionId(
                row.WorkflowDefinitionId),
            DefinitionVersion = row.DefinitionVersion,
            OwnerTenantId = new TenantId(row.OwnerTenantId),
            Visibility = ParseVisibility(row.Visibility),
            SharedTenantIds = Array.AsReadOnly(
                row.SharedTenantIds.Select(id => new TenantId(id)).ToArray()),
            CreatedAt = FromDatabaseTimestamp(row.CreatedAt)
        };

    private static WorkflowVisibility ParseVisibility(string value)
    {
        if (!Enum.TryParse<WorkflowVisibility>(
                value,
                ignoreCase: false,
                out var visibility) ||
            !Enum.IsDefined(visibility))
        {
            throw new InvalidOperationException(
                $"Stored workflow visibility '{value}' is invalid.");
        }

        return visibility;
    }

    private static DateTime ToDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime FromDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed class WorkflowSharingRow
    {
        public Guid Id { get; init; }

        public Guid WorkflowDefinitionId { get; init; }

        public required string DefinitionVersion { get; init; }

        public Guid OwnerTenantId { get; init; }

        public required string Visibility { get; init; }

        public required Guid[] SharedTenantIds { get; init; }

        public DateTime CreatedAt { get; init; }
    }
}
