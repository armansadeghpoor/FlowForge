# Database Migration Strategy

## Current Foundation

FlowForge uses ordered, SQL-first PostgreSQL migrations owned by
`FlowForge.Infrastructure`. Migration scripts are stored under:

`src/FlowForge.Infrastructure/Persistence/PostgreSql/Scripts`

Scripts follow the `<id>_<name>.sql` convention. Identifiers are zero-padded so
ordinal sorting also represents execution order. The current baseline is:

`001_initial_schema.sql`

SQL scripts are embedded in `FlowForge.Infrastructure`; deployments do not need
the source tree at runtime. The baseline still creates the complete schema used
by workflow execution, definitions, runtime coordination, audit, and workflow
sharing providers.

## Migration History

`PostgreSqlMigrationRunner` creates and maintains the
`flowforge_schema_migrations` table:

| Column | Purpose |
|---|---|
| `migration_id` | Stable migration identity and primary key |
| `migration_name` | Human-readable script name |
| `applied_at` | UTC timestamp recorded after successful execution |

The primary key prevents the same migration identity from being recorded more
than once. If a stored identity has a different name from the configured
migration, the runner fails instead of silently accepting inconsistent history.

## Migration Lifecycle

For each run, the migration runner:

1. Opens one PostgreSQL connection.
2. Acquires a PostgreSQL advisory lock dedicated to schema migration.
3. Creates the history table when it does not exist.
4. Loads applied migration identities.
5. Orders configured migrations by identifier.
6. Skips migrations already present in history.
7. Executes each pending migration in its own transaction.
8. Records the migration in history within the same transaction.
9. Releases the advisory lock.

A failed migration transaction is rolled back and is not inserted into history.
Earlier migrations that committed successfully remain applied. The advisory
lock serializes concurrent runners without introducing a runtime coordination
table.

## Executing Migrations

Migration execution is explicit and is not part of normal API startup:

```csharp
var connectionFactory = new PostgreSqlConnectionFactory(connectionString);
var migrationRunner = new PostgreSqlMigrationRunner(connectionFactory);

await migrationRunner.RunAsync(cancellationToken);
```

Deployment or trusted bootstrap code should run migrations before starting
application instances. Runtime credentials should not receive schema-change
permissions when separate migration credentials are available.

## Adding a Migration

To add a schema change:

1. Add the next zero-padded script, for example `002_add_example.sql`.
2. Keep the migration forward-only and safe to execute in a transaction.
3. Do not edit or rename a migration that may already be applied.
4. Add PostgreSQL integration coverage for clean installation and upgrade
   behavior.

The project embeds all matching SQL files automatically, and the runner applies
them in identifier order.

## Local Development

Set `FLOWFORGE_POSTGRES_CONNECTION_STRING` to a disposable PostgreSQL database.
The migration integration tests can then be run with:

```bash
dotnet test FlowForge.slnx \
  --filter FullyQualifiedName~PostgreSqlMigrationRunnerTests
```

The tests create an isolated schema, execute migrations, and remove that schema
after each test. Without the environment variable, PostgreSQL integration tests
use the existing local skip behavior.

## Existing Database Adoption

Databases previously created by directly executing `001_initial_schema.sql` do
not contain migration history. Operators must verify that the installed schema
matches the baseline and explicitly baseline migration `001` before using the
runner. The runner must not be pointed at such a database blindly because the
baseline contains non-idempotent table creation statements.

For disposable local environments, recreating the database and allowing the
runner to apply the baseline is preferred.

## Change Safety and Future Hardening

Schema changes should use expand-and-contract deployment practices. Additive
changes should be deployed before dependent code, data backfills should be
observable, and destructive changes require backups and restore validation.

Future hardening may add script checksums, a dedicated migration executable,
upgrade tests from supported release baselines, and stricter separation of
migration and runtime credentials. No automatic API-startup migration behavior
is introduced by this foundation.
