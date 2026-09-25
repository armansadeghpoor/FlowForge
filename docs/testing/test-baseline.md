# Test Baseline

## Baseline

This baseline was recorded on September 25, 2026 after Phase 10.6 architecture
verification was added.

`dotnet build FlowForge.slnx` completed successfully with:

- 0 warnings
- 0 errors

`dotnet test FlowForge.slnx` completed successfully with:

| Test assembly | Passed | Skipped | Failed | Total |
| --- | ---: | ---: | ---: | ---: |
| `FlowForge.Core.Tests` | 89 | 0 | 0 | 89 |
| `FlowForge.Application.Tests` | 33 | 0 | 0 | 33 |
| `FlowForge.Infrastructure.Tests` | 70 | 50 | 0 | 120 |
| `FlowForge.Nodes.Tests` | 41 | 0 | 0 | 41 |
| `FlowForge.Api.Tests` | 16 | 0 | 0 | 16 |
| **Total** | **249** | **50** | **0** | **299** |

## PostgreSQL Test Behavior

PostgreSQL integration tests require the
`FLOWFORGE_POSTGRES_CONNECTION_STRING` environment variable. When it is not
configured, the tests use the repository's established skippable-test behavior
and report as skipped rather than attempting to contact a local database.

The skipped tests cover PostgreSQL state-store conformance, workflow definition
round trips, persistent execution-request idempotency, schedule occurrence
tracking, execution ownership, and snapshot enumeration. Their in-memory
counterparts and all provider-independent tests remain active locally.

The GitHub Actions PostgreSQL workflow supplies the connection string to a
PostgreSQL 17 service, so provider tests execute rather than skip in that
environment.

## Architecture Verification

Dedicated dependency tests verify that:

- `FlowForge.Application` does not reference `FlowForge.Engine` or
  `FlowForge.Infrastructure`.
- `FlowForge.Api` does not reference `FlowForge.Engine` or
  `FlowForge.Infrastructure`.

These tests inspect compiled assembly references. They complement project-file
review and fail if a forbidden direct reference is introduced.

## Reproducing the Baseline

Run from the repository root:

```bash
dotnet build FlowForge.slnx
dotnet test FlowForge.slnx
```

To execute PostgreSQL tests locally, provide a reachable test database through
`FLOWFORGE_POSTGRES_CONNECTION_STRING`. Tests create and remove isolated schemas
inside that database; the configured account therefore needs schema creation
and deletion permissions.
