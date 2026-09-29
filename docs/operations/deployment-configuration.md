# Deployment Configuration

FlowForge.Api is packaged as a Linux container using the repository-root Docker build context. The image build restores and publishes the API's project dependency graph with the .NET 10 SDK, then copies only the published output into the ASP.NET Core runtime image. The final container runs as the non-root user supplied by the Microsoft runtime image and listens on port `8080`.

Build the image from the repository root:

```shell
docker build --file src/FlowForge.Api/Dockerfile --tag flowforge-api .
```

## Required environment variables

ASP.NET Core maps double underscores in environment-variable names to configuration section separators. A production deployment must provide:

| Variable | Requirement | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` | Selects production configuration and environment reporting. |
| `FlowForge__Database__ConnectionString` | Required | Supplies the database connection string consumed by the Infrastructure boundary. Do not store it in an image or source-controlled settings file. |

When authentication is required, also provide all of the following:

| Variable | Value |
| --- | --- |
| `FlowForge__Security__RequireAuthentication` | `true` |
| `FlowForge__Security__Authority` | Absolute HTTP or HTTPS authority URI for the external identity provider. Use HTTPS in production. |
| `FlowForge__Security__Audience` | Audience expected in bearer access tokens. |

The container sets `ASPNETCORE_HTTP_PORTS=8080`. A deployment may override it when its platform requires a different internal port.

## Optional production overrides

The checked-in settings provide conservative runtime and execution defaults. Override them through environment variables when the deployment requires different values:

- `FlowForge__Runtime__OwnerId`
- `FlowForge__Runtime__HeartbeatInterval`
- `FlowForge__Runtime__StaleExecutionThreshold`
- `FlowForge__Execution__DefaultNodeTimeout`
- `FlowForge__Execution__MaxNodeAttempts`

Time spans use the .NET configuration format, for example `00:00:30`. `HeartbeatInterval` must be positive, `StaleExecutionThreshold` must exceed it, `DefaultNodeTimeout` must be positive, and `MaxNodeAttempts` must be greater than zero.

The default security configuration is anonymous-friendly. If `FlowForge__Security__RequireAuthentication` remains `false`, Authority and Audience may remain empty. Selected permission-protected workflow endpoints still require an authenticated bearer token with the applicable permission.

## Startup sequence

1. ASP.NET Core loads `appsettings.json`, `appsettings.Production.json`, and environment-variable overrides.
2. FlowForge binds runtime, database, execution, and security options.
3. Startup validation rejects missing or invalid required configuration before the host begins serving requests.
4. The API registers its application services, authentication, hosting metadata, diagnostics, and middleware pipeline.
5. The host starts listening on the configured HTTP port and emits its startup lifecycle log.

The host does not run database migrations. The database schema must be provisioned separately before deployment.

## Health and readiness endpoints

Use `GET /health/live` to determine whether the API process is running:

```shell
curl --fail http://localhost:8080/health/live
```

Liveness never probes PostgreSQL or another external dependency. A successful
response means the process can answer HTTP requests; it does not mean the
application can perform database-backed work.

Use `GET /health/ready` to determine whether the API can serve application
requests:

```shell
curl --fail http://localhost:8080/health/ready
```

When `FlowForge__Database__ConnectionString` is configured, readiness opens a
PostgreSQL connection and executes a lightweight `SELECT 1` query. The endpoint
returns HTTP `200` when every configured dependency is healthy and HTTP `503`
when any dependency is unavailable.

Both responses include stable status, application name, environment, version,
request correlation identifier, and safe dependency status where applicable.
They never include connection strings, credentials, exceptions, or stack
traces. Supply `X-Correlation-Id` to preserve a caller-provided identifier.

`GET /health` remains available as the backward-compatible process-level health
endpoint and, like liveness, does not probe dependencies.
