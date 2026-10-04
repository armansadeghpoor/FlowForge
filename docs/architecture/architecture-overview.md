# FlowForge Architecture Overview

## Purpose

FlowForge is a workflow execution framework. It separates domain state,
framework contracts, orchestration, plugins, persistence, application use
cases, and transport concerns so that each can evolve without reversing the
dependency direction.

This document describes the architecture stabilized at Phase 10.6. Recovery
execution, background hosting, and external observability integrations remain
outside the implemented runtime.

## Project Responsibilities

| Project | Responsibility |
| --- | --- |
| `FlowForge.Core` | Dependency-free domain records, identifiers, statuses, failures, workflow definitions, graph models, execution snapshots, triggers, schedules, and history models. |
| `FlowForge.Abstractions` | Provider-independent ports for execution, nodes, persistence, definitions, triggers, schedules, events, validation, runtime coordination, and queries. |
| `FlowForge.Engine` | Workflow orchestration, graph validation and topological execution, middleware composition, retry and timeout policies, trigger/event/scheduler runtimes, definition validation, and query composition. |
| `FlowForge.Nodes` | Generic node-runner registry and built-in node plugins. Nodes implement behavior without owning orchestration policy. |
| `FlowForge.Infrastructure` | In-memory and PostgreSQL adapters for state, definitions, triggers, schedules, coordination, and execution queries. Dapper and Npgsql remain isolated here. |
| `FlowForge.Application` | Use-case services, validation/persistence coordination, application results, execution commands and queries, scheduling/event commands, and diagnostics aggregation. |
| `FlowForge.Api` | ASP.NET Core transport boundary, DTO mapping, HTTP error mapping, controller endpoints, and application-service registration. |
| `FlowForge.Console` | Minimal executable host boundary; it does not contain workflow orchestration logic. |

## Dependency Direction

Dependencies point inward toward stable contracts and domain types:

```text
API ───────────────> Application ──> Abstractions ──> Core
 │                                      ^              ^
 ├──────────────────────────────────────┘              │
 └─────────────────────────────────────────────────────┘

Engine ────────────> Abstractions ────────────────────> Core
Nodes ─────────────> Abstractions ────────────────────> Core
Infrastructure ────> Abstractions ────────────────────> Core
```

The diagram expresses architectural direction rather than every direct project
reference. In particular, Application references only Abstractions and Core,
and API references Application, Abstractions, and Core. Dependency tests guard
against Application or API acquiring references to Engine or Infrastructure.

Core has no package or project dependencies. Infrastructure owns the current
third-party persistence dependencies. Engine operates against abstractions and
does not select concrete storage providers.

## Execution Lifecycle

1. An execution request enters through a direct engine call or a trigger
   runtime. `ExecutionRequestId` supplies idempotency identity and
   `ExecutionCorrelationId` supplies lifecycle correlation.
2. `WorkflowEngine` delegates to `WorkflowExecutor`; it does not contain node
   execution logic.
3. The executor validates the supplied definition and constructs its
   `WorkflowGraph`. Invalid graphs do not enter execution.
4. A `Running` `WorkflowExecution` snapshot is created with its immutable
   definition version and correlation metadata. Creation history is appended.
5. `TopologicalSorter` produces dependency layers. Nodes within one layer may
   execute concurrently; later layers wait for the current layer.
6. Each node is resolved through `INodeRunnerRegistry` and invoked through
   `ExecutionPipeline`. Middleware owns retry and timeout behavior. The node
   runner owns only node behavior.
7. Node snapshots, attempts, outputs, typed failures, timestamps, and history
   are persisted through `IStateStore`.
8. A failed layer stops later layers. The workflow snapshot is finalized as
   `Succeeded` or `Failed`, and completion history is appended.

Execution history is append-only audit information. The current execution
snapshot remains the authoritative state; FlowForge is not event sourced.

### Execution outcome semantics

The execution lifecycle distinguishes node results, caller cancellation,
execution exceptions, and persistence failures:

| Outcome | Node terminal state | Workflow behavior |
| --- | --- | --- |
| Successful `NodeExecutionResult` | `Succeeded` | Execution may continue to the next dependency layer. |
| Failed `NodeExecutionResult` | `Failed` | The supplied `NodeFailure` category is preserved and later layers do not begin. |
| Policy timeout | `Failed` | Timeout remains a failed node result with `NodeFailureCategory.Cancelled`; it is not caller cancellation. |
| Caller cancellation | `Cancelled` | Started node and workflow executions are finalized as cancelled where persistence remains available, cancellation history is recorded, and `OperationCanceledException` is rethrown. |
| Unexpected node or middleware exception | `Failed` | The node failure is classified as `NodeFailureCategory.Execution`; it is not converted to cancellation. |
| Persistence failure | Not reclassified | The infrastructure failure propagates and is never represented as a node failure. |

`Succeeded`, `Failed`, and `Cancelled` are terminal workflow and node states.
`Pending` and `Running` remain non-terminal states already present in the
domain model. `WorkflowCancelled` and `NodeCancelled` are the corresponding
append-only history events.

Policy timeout and caller cancellation intentionally have different control
flow. A policy timeout is a normal failed `NodeExecutionResult`; caller
cancellation remains exceptional control flow and must be rethrown only after
durable cancellation finalization.

Phase 14.1 defined this semantic contract and added the cancellation history
event vocabulary. Phase 14.2 introduces an Engine-internal node execution
coordinator around the existing pipeline. The coordinator converts unexpected
node, middleware, and pipeline exceptions into a safe failed result with
`NodeFailureCategory.Execution`. That result follows the existing terminal
node snapshot and `NodeFailed` history path; exception messages and stack
traces are not persisted.

Caller cancellation bypasses execution-failure classification and continues
to propagate as `OperationCanceledException`. Persistence before, during, or
after execution is also kept outside node-failure classification: an internal
marker identifies attempt-state persistence performed from the pipeline
delegate, and the original persistence exception is rethrown by the
coordinator.

Concurrent layers preserve the existing `Task.WhenAll` model. Every node in a
started layer is observed before the layer result is evaluated. A returned or
coordinated node failure does not cancel its siblings: already-terminal sibling
states are preserved, running siblings finish normally, and later graph layers
do not begin. Multiple failures are retained on their individual node states in
deterministic layer order. `WorkflowExecution` has no single workflow-failure
payload, so workflow aggregation records the terminal `Failed` status and
`WorkflowFailed` history event without selecting or discarding one node
failure as primary.

Caller cancellation has precedence while execution is active. Each started,
still-running node is persisted as `Cancelled` with a cancelled failure and a
`NodeCancelled` history event. Siblings that already reached `Succeeded` or
`Failed` remain unchanged. After all started layer tasks have been observed,
the workflow is persisted as `Cancelled`, `WorkflowCancelled` is appended, and
the original `OperationCanceledException` is rethrown. Cancellation
finalization uses a token independent from the already-cancelled caller token.

Persistence failures remain outside execution classification and propagate to
the caller, including failures during cancellation finalization.

### Atomic lifecycle persistence

Phase 14.4 introduces `IStateStore.ExecuteLifecycleAsync` and its scoped,
provider-independent `IExecutionLifecycleTransaction` write handle. Each callback
contains only sequential, awaited persistence writes for one workflow execution.
Handles must not escape the callback, exceptions must propagate, and callbacks
must not invoke the parent store recursively. The Engine uses these scopes for:

- Workflow creation together with `WorkflowCreated` and `WorkflowStarted`.
- A running node snapshot together with `NodeStarted`.
- A succeeded, failed, or cancelled node snapshot together with its corresponding
  `NodeCompleted`, `NodeFailed`, or `NodeCancelled` event.
- Workflow terminal status and timestamps together with `WorkflowCompleted`,
  `WorkflowFailed`, or `WorkflowCancelled`.

Retry attempt updates remain ordinary snapshot writes because they have no
corresponding history event. Existing standalone state/history methods remain
available; calling them separately does not provide a combined atomic transition.
Custom `IStateStore` providers must implement the new atomic callback contract;
there is no non-atomic default implementation.

PostgreSQL opens one connection and one transaction per lifecycle callback. Every
participating Dapper command uses that same connection and transaction. Commit
occurs only after the callback succeeds; a write or callback failure rolls back
the transaction. Rollback uses an independent cancellation token and cannot
replace the original failure. No schema or migration changes are required.
History retains its existing ordering by timestamp and database append sequence;
rolled-back inserts can leave harmless sequence gaps.

The in-memory provider stages state and history in a private snapshot and publishes
the complete pair with one reference assignment only after success. A semaphore
per execution serializes writes to that execution, including standalone writes,
without blocking unrelated executions. Reads see committed snapshots. Failed or
cancelled callbacks publish nothing. History retains timestamp order with stable
append order for ties. Two separate read calls may straddle a commit in either
provider; they are not a combined transactional read API.

Transactions are scoped to persistence transitions, never entire workflows.
No transaction spans node execution, HTTP calls, delays, retries, or waiting for
concurrent siblings. The existing `Task.WhenAll` behavior remains intact. Caller
cancellation cleanup uses the existing independent token, so each cancellation
snapshot and history event can commit together before cancellation is rethrown.
If cleanup persistence fails, the original persistence failure propagates.

Atomicity does not guarantee eventual finalization: a failed terminal transition
leaves the previous committed state/history pair, which can still be `Running`.
Independent siblings and earlier transitions stay committed. Connection loss
during commit can leave its acknowledgement uncertain even though the database
transition itself is atomic. No automatic retry or recovery is introduced.

For Phase 14.5 planning, unresolved responsibilities include reconciling interrupted
executions and uncertain commit outcomes, and any recovery/idempotency policy for
external node effects. Phase 14.4 does not define or implement that later scope.

## Trigger Runtime Architecture

All trigger paths converge on the existing workflow engine rather than
duplicating execution behavior:

```text
Manual request ─────┐
Due schedule ───────┼─> IWorkflowTriggerExecutor ─> WorkflowEngine
Matching event ─────┘
```

- Manual execution loads an enabled manual trigger and its exact definition
  version before calling the engine.
- The scheduler evaluates enabled schedule metadata and delegates due trigger
  occurrences through `IWorkflowTriggerExecutor`. It is not a hosted worker.
- The event runtime matches event types and delegates each matching trigger.
  It does not provide a message broker or consumer runtime.
- `IExecutionRequestStore` prevents duplicate execution requests.
- `IScheduleExecutionTracker` prevents duplicate schedule occurrences.
- `IExecutionOwnershipStore` provides an atomic ownership boundary for running
  executions.

In-memory coordination is available for local operation and tests. PostgreSQL
adapters provide persistent, multi-process coordination through conditional
inserts and updates without distributed locks.

## Persistence Boundaries

`IStateStore` persists workflow and node snapshots, lifecycle changes,
heartbeats, ownership metadata, stale-execution queries, correlation queries,
and append-only execution history. Reads reconstruct complete execution
aggregates including node states.

Definition, trigger, schedule, event-trigger, request-idempotency, schedule
occurrence, and execution-ownership responsibilities use separate contracts.
This prevents the state store from becoming a general repository boundary.

`IExecutionSnapshotQuery` is a read-only enumeration boundary used by query and
diagnostics composition. It does not add write responsibilities to
`IStateStore`.

The in-memory providers preserve immutable snapshot semantics and support fast
tests. PostgreSQL providers use the existing connection factory, Dapper, and
Npgsql. Provider conformance tests define shared state-store behavior.

## Observability Foundation

Observability is currently an internal query capability:

- `ExecutionSummary` derives lifecycle, recovery, definition-version, and node
  count information from execution snapshots.
- Timeline queries expose ordered execution history.
- Correlation queries find executions sharing an `ExecutionCorrelationId`.
- `RuntimeDiagnosticsService` calculates total, running, succeeded, and failed
  execution counts, average recorded duration, and the latest start timestamp.
- `GET /api/diagnostics/executions` exposes the immutable metrics snapshot.

There is no logging pipeline, metrics exporter, OpenTelemetry integration,
Prometheus endpoint, dashboard, or infrastructure health probe. Those remain
future adapters around the existing internal query boundary.

## Stabilized Boundaries

The current architecture deliberately keeps these concerns separate:

- Nodes do not implement orchestration, retry, timeout, or persistence.
- Engine does not select infrastructure providers or expose HTTP concerns.
- Application does not reference Engine or Infrastructure.
- API does not reference Engine or Infrastructure.
- Persistence snapshots remain authoritative while history remains diagnostic.
- Stale detection and ownership coordination do not constitute a recovery
  runtime.

Future production work should preserve these boundaries and add capabilities
through contracts and adapters rather than cross-layer references.
