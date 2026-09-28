namespace FlowForge.Infrastructure.Persistence.PostgreSql.Migrations;

/// <summary>
/// Describes one ordered PostgreSQL schema migration.
/// </summary>
/// <param name="Id">The stable, sortable migration identifier.</param>
/// <param name="Name">The descriptive migration name.</param>
/// <param name="Sql">The SQL executed by the migration.</param>
public sealed record PostgreSqlMigration(
    string Id,
    string Name,
    string Sql);
