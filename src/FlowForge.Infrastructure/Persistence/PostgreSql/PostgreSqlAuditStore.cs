using System.Text.Json;
using Dapper;
using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using Npgsql;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Stores append-only enterprise audit entries in PostgreSQL.
/// </summary>
public sealed class PostgreSqlAuditStore : IAuditStore
{
    private const string InsertSql =
        """
        INSERT INTO audit_entries
            (id, user_id, tenant_id, resource_tenant_id, action, resource_type,
             resource_identifier, outcome, correlation_id, occurred_at, metadata)
        VALUES
            (@Id, @UserId, @TenantId, @ResourceTenantId, @Action, @ResourceType,
             @ResourceIdentifier, @Outcome, @CorrelationId, @OccurredAt,
             CAST(@Metadata AS jsonb));
        """;

    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL enterprise audit store.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlAuditStore(PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AppendAsync(
        AuditEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertSql,
                new
                {
                    Id = entry.Id.Value,
                    entry.UserId,
                    TenantId = entry.TenantId?.Value,
                    ResourceTenantId = entry.ResourceTenantId?.Value,
                    entry.Action,
                    entry.ResourceType,
                    entry.ResourceIdentifier,
                    Outcome = entry.Outcome.ToString(),
                    CorrelationId = entry.CorrelationId.Value,
                    OccurredAt = ToDatabaseTimestamp(entry.Timestamp),
                    Metadata = entry.Metadata?.GetRawText()
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                  exception.ConstraintName == "audit_entries_pkey")
        {
            throw new InvalidOperationException(
                $"Audit entry '{entry.Id.Value}' already exists.",
                exception);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        const string sql =
            """
            SELECT id AS Id,
                   user_id AS UserId,
                   tenant_id AS TenantId,
                   resource_tenant_id AS ResourceTenantId,
                   action AS Action,
                   resource_type AS ResourceType,
                   resource_identifier AS ResourceIdentifier,
                   outcome AS Outcome,
                   correlation_id AS CorrelationId,
                   occurred_at AS OccurredAt,
                   metadata::text AS Metadata
            FROM audit_entries
            WHERE (CAST(@TenantId AS uuid) IS NULL
                   OR tenant_id = @TenantId
                   OR resource_tenant_id = @TenantId)
              AND (CAST(@CorrelationId AS uuid) IS NULL
                   OR correlation_id = @CorrelationId)
              AND (CAST(@ResourceType AS text) IS NULL
                   OR resource_type = @ResourceType)
              AND (CAST(@ResourceIdentifier AS text) IS NULL
                   OR resource_identifier = @ResourceIdentifier)
            ORDER BY append_sequence;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditEntryRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = query.TenantId?.Value,
                    CorrelationId = query.CorrelationId?.Value,
                    query.ResourceType,
                    query.ResourceIdentifier
                },
                cancellationToken: cancellationToken));

        return Array.AsReadOnly(rows.Select(MapEntry).ToArray());
    }

    private static AuditEntry MapEntry(AuditEntryRow row) =>
        new()
        {
            Id = new AuditEntryId(row.Id),
            UserId = row.UserId,
            TenantId = row.TenantId is { } tenantId
                ? new TenantId(tenantId)
                : null,
            ResourceTenantId = row.ResourceTenantId is { } resourceTenantId
                ? new TenantId(resourceTenantId)
                : null,
            Action = row.Action,
            ResourceType = row.ResourceType,
            ResourceIdentifier = row.ResourceIdentifier,
            Outcome = ParseOutcome(row.Outcome),
            CorrelationId = new ExecutionCorrelationId(row.CorrelationId),
            Timestamp = FromDatabaseTimestamp(row.OccurredAt),
            Metadata = row.Metadata is null
                ? null
                : JsonSerializer.Deserialize<JsonElement>(row.Metadata)
        };

    private static AuditOutcome ParseOutcome(string value)
    {
        if (!Enum.TryParse<AuditOutcome>(value, ignoreCase: false, out var outcome) ||
            !Enum.IsDefined(outcome))
        {
            throw new InvalidOperationException(
                $"Stored audit outcome '{value}' is invalid.");
        }

        return outcome;
    }

    private static DateTime ToDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime FromDatabaseTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed class AuditEntryRow
    {
        public Guid Id { get; init; }

        public string? UserId { get; init; }

        public Guid? TenantId { get; init; }

        public Guid? ResourceTenantId { get; init; }

        public required string Action { get; init; }

        public required string ResourceType { get; init; }

        public required string ResourceIdentifier { get; init; }

        public required string Outcome { get; init; }

        public Guid CorrelationId { get; init; }

        public DateTime OccurredAt { get; init; }

        public string? Metadata { get; init; }
    }
}
