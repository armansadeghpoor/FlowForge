# Logging Conventions

FlowForge uses structured Microsoft Extensions Logging events at framework and host boundaries. Logging remains provider-neutral; hosts may select a logging provider without changing Core, Abstractions, Application, Engine, or node implementations.

## Event naming

- Use stable, present-tense event descriptions such as `HTTP request completed`, `Workflow execution started`, and `Application stopping`.
- Prefer structured message-template properties over string interpolation.
- Use consistent property names across events, including `CorrelationId`, `WorkflowExecutionId`, `NodeExecutionId`, `StatusCode`, and `DurationMilliseconds`.
- Log lifecycle transitions once at the component that owns the transition.

## Correlation

- Every HTTP request carries `X-Correlation-Id`; the API preserves a supplied value or generates one.
- Request, application, execution, and failure logs should include `CorrelationId` whenever it is available.
- Correlation identifiers connect related records but do not establish identity, authorization, idempotency, or ordering.
- Do not place secrets or personal data in correlation identifiers.

## Logging boundaries

- The API host logs HTTP method, path, response status, duration, and correlation identifier after request completion.
- Hosting logs application startup and shutdown.
- Application services may log use-case outcomes without exposing provider exceptions or sensitive inputs.
- Infrastructure providers may log provider operations, but must not leak credentials, connection strings, or persisted payloads.
- Engine and node logs should describe orchestration or node lifecycle behavior without duplicating API request logs.

## Sensitive data

Do not log request or response bodies, authorization headers, cookies, database connection strings, credentials, tokens, or arbitrary node configuration. Header logging must use an explicit allowlist; the operational request logger currently logs no headers other than the separately established correlation identifier.

## Operational metrics

FlowForge records provider-independent counters and duration observations through `IMetricsCollector`. The in-memory host implementation exposes immutable snapshots through `GET /api/v1/diagnostics/runtime`.

Stable metric names use lowercase dot-separated segments:

| Metric | Kind | Meaning |
|---|---|---|
| `flowforge.workflow_definitions.created` | Counter | Successfully created definition versions |
| `flowforge.workflow_definitions.read` | Counter | Completed definition read operations |
| `flowforge.workflow_definitions.listed` | Counter | Completed definition list operations |
| `flowforge.workflow_executions.started` | Counter | Executions that entered the running state |
| `flowforge.workflow_executions.completed` | Counter | Executions that reached a terminal state |
| `flowforge.workflow_executions.succeeded` | Counter | Successfully completed executions |
| `flowforge.workflow_executions.failed` | Counter | Failed executions |
| `flowforge.workflow_executions.duration` | Duration | Terminal execution duration |
| `flowforge.authorization.allowed` | Counter | Allowed authorization decisions |
| `flowforge.authorization.denied` | Counter | Denied authorization decisions |
| `flowforge.audit.events.recorded` | Counter | Audit entries appended successfully |
| `flowforge.audit.events.failed` | Counter | Audit entries that could not be appended |
| `http.server.requests` | Counter | Completed HTTP requests |
| `http.server.responses.<category>` | Counter | Responses grouped into bounded `1xx` through `5xx` categories |
| `http.server.responses.<status-code>` | Counter | Responses grouped by exact HTTP status code for compatibility |
| `http.server.request.duration` | Duration | HTTP request duration |

Metric names and values are aggregate operational data. They must not contain tenant identifiers, user identifiers, workflow or resource identifiers, payloads, configuration values, credentials, tokens, request bodies, or sensitive headers. Tenant context remains optional and is deliberately not encoded into the in-memory metric key space, avoiding tenant data disclosure and unbounded metric cardinality.
