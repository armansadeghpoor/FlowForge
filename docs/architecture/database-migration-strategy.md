# Database Migration Strategy

## Current Approach

FlowForge currently maintains one SQL bootstrap script:

`src/FlowForge.Infrastructure/Persistence/PostgreSql/Scripts/001_initial_schema.sql`

The script creates the complete PostgreSQL schema required by the current
providers. PostgreSQL integration tests create an isolated schema and execute
the script before running. This gives new databases and tests a deterministic
starting point.

The current approach is intentionally a bootstrap mechanism, not a migration
system. The runtime does not:

- track an installed schema version;
- discover or order multiple migrations;
- upgrade an existing database;
- roll back a schema change;
- coordinate concurrent migration runners; or
- invoke schema changes during application startup.

Editing the initial script is acceptable while databases are disposable. It is
not safe once persistent environments may already contain an earlier schema.

## Requirements for a Future Strategy

A production migration strategy should provide:

1. An append-only sequence of uniquely numbered, immutable migration scripts.
2. A schema-history table recording the identifier, checksum, and application
   time of every migration.
3. Deterministic ordering and failure on missing, duplicated, or modified
   applied scripts.
4. Transactional execution when PostgreSQL supports the included operations.
5. A single migration owner per environment to prevent concurrent upgrades.
6. Pre-deployment validation, backups, and a documented restore procedure.
7. Integration tests that build an empty database and upgrade a database from
   every supported release baseline.
8. Separate credentials so normal runtime connections cannot alter schema.

## Strategy Options

### Application-managed SQL runner

FlowForge could add a small migration executable that reads ordered embedded
SQL scripts and maintains a schema-history table. This keeps migrations close
to the repository and supports SQL review, but FlowForge would own ordering,
checksums, concurrency, diagnostics, and failure recovery.

### Established .NET migration library

A dedicated migration library can provide script discovery, version tracking,
and execution. This reduces custom infrastructure but adds a production
dependency and requires evaluation of transactional behavior, checksum policy,
deployment integration, and long-term maintenance.

### Deployment-managed migration tool

An external deployment tool can apply versioned SQL before the application is
released. This keeps schema permissions and rollout control outside the
runtime, but local development and CI must use the same scripts and ordering to
avoid environment drift.

The choice should be made with the deployment model. FlowForge should not run
uncoordinated migrations from every API or worker instance.

## Recommended Evolution

Retain SQL-first migrations because the PostgreSQL adapters already use Dapper
and explicit SQL. Before the first non-disposable production database:

1. Freeze `001_initial_schema.sql` as the immutable baseline.
2. Select either a deployment-managed runner or a dedicated migration
   executable.
3. Introduce a schema-history table and checksum validation.
4. Add new numbered scripts rather than editing applied scripts.
5. Test both clean installation and incremental upgrades in PostgreSQL CI.
6. Make migration execution an explicit deployment step, separate from normal
   API and execution startup.

## Change Safety

Prefer expand-and-contract changes for live environments:

- add nullable columns or new tables before code starts using them;
- deploy code capable of reading old and new representations when needed;
- backfill data in a separately observable step;
- enforce new constraints only after data is compatible; and
- remove obsolete structures in a later release.

Destructive changes require a backup, restore validation, and an explicit
rollback or forward-fix plan. Application rollback does not automatically undo
schema changes.

## Current Decision

No migration implementation is introduced in Phase 10.6. The initial script
continues to bootstrap disposable databases and isolated test schemas. Adding a
production database or retaining data across releases is the trigger for
adopting the versioned strategy above.
