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

## Health endpoint

Use `GET /health` for an application-level liveness check:

```shell
curl --fail http://localhost:8080/health
```

The response includes application status, environment name, and application version. This endpoint intentionally does not probe PostgreSQL or other external dependencies, so it is a liveness signal rather than a dependency-readiness guarantee.
