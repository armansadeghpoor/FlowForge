# CI/CD Foundation

FlowForge uses GitHub Actions for continuous integration. The workflow validates every push and pull request but does not deploy the application, publish a container, or interact with a cloud provider.

## Pipeline stages

The `.github/workflows/ci.yml` workflow runs one `build-and-test` job on the latest Ubuntu runner:

1. **Checkout** retrieves the repository with read-only contents permission.
2. **SDK setup** installs the latest available .NET 10 SDK feature patch.
3. **PostgreSQL preparation** starts a PostgreSQL 17 service and waits for `pg_isready` to report a healthy database.
4. **Restore** runs `dotnet restore FlowForge.slnx`.
5. **Build** compiles the solution in Release configuration without restoring again.
6. **Test** runs the solution test projects against the completed Release build. The PostgreSQL connection string enables provider integration tests that otherwise skip during local runs without a database.
7. **Test-result publication** uploads generated TRX files as the `test-results` workflow artifact, including when the test step fails. Artifacts are retained for 14 days.

Any restore, build, or test failure fails the job. PostgreSQL provider failures therefore fail CI rather than appearing as skipped tests.

The existing PostgreSQL-specific workflow remains independent. The consolidated CI workflow is the general pull-request and push quality gate.

## Required configuration

The workflow uses an isolated service database with CI-only credentials:

```text
Database: flowforge
Username: postgres
Password: postgres
Port: 5432
```

The test process receives:

```text
FLOWFORGE_POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=flowforge;Username=postgres;Password=postgres
```

These values are limited to the ephemeral GitHub Actions job and are not production credentials. No repository secret is required for the current CI workflow. Tests create and remove isolated schemas, so an alternative local database account must have schema creation and deletion permissions.

The workflow also requires access to GitHub-hosted actions and public .NET/NuGet and PostgreSQL container sources. It requests only read access to repository contents.

## Local equivalent

From the repository root, provide a reachable PostgreSQL test database and run:

```shell
export FLOWFORGE_POSTGRES_CONNECTION_STRING='Host=localhost;Port=5432;Database=flowforge;Username=postgres;Password=postgres'
dotnet restore FlowForge.slnx
dotnet build FlowForge.slnx --configuration Release --no-restore
dotnet test FlowForge.slnx \
  --configuration Release \
  --no-build \
  --no-restore \
  --logger trx \
  --results-directory TestResults
```

If `FLOWFORGE_POSTGRES_CONNECTION_STRING` is omitted, provider-independent tests still run and the PostgreSQL integration tests report as skipped. Local TRX files are written beneath `TestResults` and are excluded from the Docker build context.

## Scope

This foundation performs continuous integration only. It does not contain deployment jobs, cloud credentials, container registry publication, Kubernetes resources, Helm charts, or environment promotion logic.
