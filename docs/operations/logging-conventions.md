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
