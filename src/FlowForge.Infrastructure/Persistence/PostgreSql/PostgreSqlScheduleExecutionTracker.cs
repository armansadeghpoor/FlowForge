using Dapper;
using FlowForge.Abstractions.Scheduling;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Atomically tracks schedule occurrences in PostgreSQL.
/// </summary>
public sealed class PostgreSqlScheduleExecutionTracker : IScheduleExecutionTracker
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new PostgreSQL schedule execution tracker.
    /// </summary>
    /// <param name="connectionFactory">The factory used to create database connections.</param>
    public PostgreSqlScheduleExecutionTracker(PostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastTrackedOccurrenceAsync(
        WorkflowScheduleId scheduleId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT MAX(occurrence_at)
            FROM schedule_executions
            WHERE schedule_id = @ScheduleId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var occurrence = await connection.QuerySingleAsync<DateTime?>(new CommandDefinition(
            sql,
            new { ScheduleId = scheduleId.Value },
            cancellationToken: cancellationToken));
        return occurrence is null
            ? null
            : DateTime.SpecifyKind(occurrence.Value, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public async Task<bool> TryTrackAsync(
        WorkflowScheduleId scheduleId,
        DateTime occurrenceUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO schedule_executions (schedule_id, occurrence_at)
            VALUES (@ScheduleId, @OccurrenceAt)
            ON CONFLICT (schedule_id, occurrence_at) DO NOTHING;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ScheduleId = scheduleId.Value,
                OccurrenceAt = DateTime.SpecifyKind(occurrenceUtc, DateTimeKind.Unspecified)
            },
            cancellationToken: cancellationToken));
        return affectedRows == 1;
    }
}
