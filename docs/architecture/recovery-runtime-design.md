# Recovery Runtime Design

## Purpose and Scope

This document describes a future runtime design for recovering interrupted
FlowForge executions. It builds on snapshot persistence, heartbeat metadata,
stale detection, ownership claiming, definition versions, execution history,
and observability queries.

This is a design only. FlowForge does not currently recover or resume an
execution automatically. Execution snapshots remain the authoritative current
state; history remains an audit and diagnostic aid rather than an event-sourced
state model.

## 1. Recovery Lifecycle

Recovery should proceed through explicit, separately testable stages.

### 1. Stale detection

A recovery worker queries for `Running` executions whose heartbeat is absent or
older than an operational threshold. Staleness produces candidates only. It is
not a workflow status and does not prove that the current owner is dead.

Before continuing, the worker must re-check that the execution is still
`Running` and that its heartbeat has not become fresh. Threshold selection and
owner-loss determination are deployment policy, not workflow business logic.

### 2. Ownership claim

Exactly one recovery owner must acquire authority before analysis can lead to
execution. Claiming must be atomic and conditional on the latest persisted
state. A failed claim means another owner won or the execution is no longer
eligible, so that candidate must not be recovered by the losing worker.

The current claim contract only claims an unowned execution. Recovering an
execution that still records a stale owner will require a future, explicitly
designed takeover or lease-expiration operation. A stale heartbeat alone must
never be used to overwrite `OwnerId` with an unconditional update.

### 3. State analysis

After claiming, the runtime loads the complete workflow execution snapshot,
node states, ordered history, recovery metadata, and the exact workflow
definition version. It then classifies completed, eligible, failed, and
ambiguous work without changing state.

### 4. Recovery decision

A recovery analyzer produces an explicit decision: Resume, Restart, or Manual
Intervention. The decision should identify its evidence, affected nodes,
idempotency assumptions, and the reason it is safe. No work executes when the
analysis is incomplete or contradictory.

### 5. Recovery execution

The recovery executor applies the approved decision while continually checking
ownership and cancellation. Resume executes only the selected unfinished work.
Restart creates a distinct execution. Manual Intervention performs no node
execution.

Node execution continues to use the normal execution pipeline. Recovery must
not bypass node runners, retry and timeout middleware, snapshot writes, or
history recording.

### 6. Finalization

The runtime persists the resulting node snapshots and final workflow snapshot,
then appends the corresponding audit history. Successful recovery ends through
the normal workflow lifecycle. Failed recovery records the classified failure
through existing execution behavior; it does not introduce a recovery-specific
workflow status.

Future ownership lifecycle semantics must also define when an owner is retained,
released, or superseded. Until that behavior exists, finalization must not
invent an ownership update outside the state-store contract.

## 2. Recovery Analysis

The analyzer must evaluate all available evidence together.

### Workflow status

- Only a `Running` workflow is a normal stale-recovery candidate.
- `Succeeded`, `Failed`, and `Cancelled` executions are terminal and must not be
  resumed implicitly.
- `Pending` is not stale running work. Starting it is normal execution, not
  crash recovery.
- The status must be read again after ownership coordination to avoid acting on
  an outdated candidate list.

### Node states

The analyzer reconstructs dependency readiness from the versioned workflow
definition and current node snapshots. It identifies nodes that are complete,
nodes that never started, recorded failures, and `Running` nodes whose actual
outcome may be unknown. Attempt numbers, typed failures, timestamps, and output
snapshots contribute evidence but do not by themselves resolve external side
effects.

### Definition version

Recovery must load the exact definition version recorded by the execution. The
graph, node types, and configuration from a newer version must not be substituted.
If the matching definition cannot be resolved or verified, automatic recovery
is unsafe and the decision must be Manual Intervention.

### Execution history

Ordered history helps explain which lifecycle writes were observed and in what
order. It can reveal missing terminal events or support investigation of an
ambiguous node. History is not the source of truth and cannot independently
rebuild the aggregate. A disagreement between history and the current snapshot
must be treated as an inconsistency requiring conservative handling.

### Ownership metadata

`OwnerId` identifies the recorded owner, while `LastHeartbeatAt` supports a
freshness assessment. Analysis must distinguish an unowned candidate, a stale
recorded owner, and an owner that has resumed heartbeats. Ownership must be
revalidated immediately before executing recovered work. Neither an old
heartbeat nor a successful read grants execution authority.

## 3. Node Recovery Semantics

### Succeeded nodes

Resume preserves succeeded nodes, their outputs, and their attempts. They are
not executed again. Restart may replay them only as part of a new execution and
only when the restart decision has addressed external-side-effect safety.

### Failed nodes

A failed node remains the recorded result of its last attempt. Resume may retry
it only when failure classification, attempt policy, and node idempotency permit
another attempt. Non-retryable or exhausted failures lead to normal workflow
failure or Manual Intervention; recovery is not an implicit retry reset.

### Pending nodes

Pending nodes have no recorded start. Resume may execute them when every
dependency is satisfied by the matching definition and the recovery plan has no
unresolved upstream ambiguity. Pending nodes downstream of an unresolved node
remain blocked.

### Running nodes after a crash

A persisted `Running` node is indeterminate. The process may have performed no
work, partial work, or all external work before losing the next snapshot write.
The analyzer must choose one of these explicit outcomes:

- reconcile with the external system and mark or continue from the observed
  result;
- retry through an idempotent operation or stable idempotency key;
- fail safely when the operation is known not to have completed; or
- require Manual Intervention when the outcome cannot be established.

The runtime must never convert every interrupted `Running` node to `Pending` and
blindly execute it again.

## 4. Recovery Modes

### Resume

Resume continues the same `WorkflowExecutionId` and preserves succeeded work,
outputs, attempts, definition version, and audit history. It schedules only
nodes selected by the recovery plan. Resume is appropriate when dependency
state is consistent and every interrupted side effect is reconciled or safely
idempotent.

### Restart

Restart creates a new workflow execution from the same immutable definition
version. The interrupted execution remains unchanged as an audit record. A
future correlation mechanism may link the new execution to the original, but
restart must not reuse or overwrite the old execution identifier. Restart does
not make duplicate external effects safe; that question must be resolved before
the new execution begins.

### Manual Intervention

Manual Intervention executes no nodes. It presents the snapshot, timeline,
definition version, ownership evidence, and ambiguity to an operator. An
operator may later approve a resume, approve a restart, reconcile an external
operation, or leave the execution unchanged. Operator actions must be explicit,
authorized, and auditable when this layer is implemented.

## 5. Safety Rules

1. **No duplicate recovery owners.** Only the winner of an atomic ownership
   operation may execute a recovery plan. Ownership must be checked again before
   work begins and during long-running recovery.
2. **No blind replay of external side effects.** An ambiguous node requires
   reconciliation or an idempotency guarantee. Retryability alone is not proof
   that replay is safe.
3. **Definition-version awareness.** Analysis and execution use the exact
   definition version recorded on the snapshot. Missing or mismatched
   definitions stop automatic recovery.
4. **Explicit handling of ambiguous running nodes.** Every interrupted
   `Running` node receives a recorded reconciliation, safe retry, safe failure,
   or manual decision. There is no default reset to `Pending`.
5. **Snapshots remain authoritative.** History supports decisions but does not
   replace snapshot state or enable event replay.
6. **Terminal executions remain terminal.** Recovery does not reopen a
   completed, failed, or cancelled workflow implicitly.
7. **Recovery uses normal execution boundaries.** Selected work flows through
   the existing node registry, execution pipeline, state store, and history
   mechanisms.

## 6. Future Components

### Recovery Worker

The Recovery Worker coordinates the lifecycle. It periodically obtains stale
candidates, revalidates them, attempts ownership, invokes analysis, dispatches
approved plans, and finalizes outcomes. Scheduling and hosting are external
concerns and are not part of the current engine.

### Recovery Analyzer

The Recovery Analyzer is a deterministic decision component. It accepts the
snapshot, exact workflow definition, history, ownership evidence, node recovery
capabilities, and policy. It returns a recovery plan or a Manual Intervention
decision without executing nodes or writing state.

### Recovery Executor

The Recovery Executor applies an approved plan through existing orchestration
boundaries. It enforces dependency order, revalidates ownership, invokes the
normal execution pipeline, persists snapshots, and records audit history. It
does not decide whether ambiguous work is safe.

### Operator/API layer

A future operator or API layer exposes recovery candidates, analysis evidence,
and proposed decisions. It accepts authorized manual choices and makes their
audit trail visible. This layer must remain outside Core and must not embed
recovery policy in transport or UI code. No operator API or user interface is
introduced by this phase.

## Current Limitations and Preconditions

The current foundation does not yet provide definition retrieval, ownership
takeover or lease expiration, node idempotency declarations, reconciliation
contracts, recovery plans, or an operator authorization model. These gaps are
preconditions for a safe runtime. Stale detection and ownership metadata alone
are intentionally insufficient to start recovery work.
