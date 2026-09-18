# Execution Recovery Strategy

## Purpose and Scope

This document defines a future recovery strategy for FlowForge. It is an
architectural guide, not a runtime recovery design. FlowForge v1 records
execution snapshots, detects potentially stale executions, and supports atomic
ownership claims, but it does not automatically recover or resume workflows.

## 1. Recovery Principles

### Snapshot-based execution state

FlowForge persists the latest `WorkflowExecution` aggregate and its
`NodeExecutionState` snapshots. These snapshots describe the most recently
recorded state; they do not prove that every external operation completed, nor
do they retain the full sequence of transitions that produced the state.

Recovery must therefore use persisted snapshots as evidence, not as a complete
event history. A recovery decision must account for the gap that can exist
between a node's external effects and the next successful state-store write.

### Staleness is a query result

Staleness is not a `WorkflowExecutionStatus`. An execution remains `Running`
while it may be reported as stale because its heartbeat is absent or older than
a supplied threshold. The threshold is an operational observation and does not
by itself prove that the original owner has stopped or that a node is safe to
run again.

### No automatic recovery in v1

FlowForge v1 must not automatically resume stale executions. Detection and
ownership claiming supply coordination metadata only. Acquiring ownership does
not establish that repeating the interrupted work is safe.

### Explicit recovery strategy

Future recovery must be initiated through an explicit strategy. That strategy
must consider the saved workflow definition version, node state, node recovery
semantics, idempotency guarantees, and available execution history before it
chooses to resume, retry, reconcile, fail, or require operator intervention.

## 2. Execution Failure Scenarios

### Process crash during node execution

The stored node may remain `Running` even though the process executing it has
stopped. The node may have performed no work, partial work, or all of its work.
Its persisted status alone cannot distinguish these outcomes.

### Crash before state persistence

A process can calculate a result or complete a transition and then crash before
the updated snapshot is saved. The state store will expose the previous state,
so a future recovery strategy must not assume that persisted state precisely
matches the last in-process state.

### External side effect completed but state not saved

A node can successfully call an external system and crash before recording
success. Re-executing that node may duplicate a payment, message, mutation, or
other side effect. Such nodes require an idempotency or reconciliation contract
before unattended recovery can be considered safe.

### Lost heartbeat

A heartbeat can be delayed or lost while the owner is still running. Stale
detection can therefore produce a recovery candidate rather than proof of
failure. A future recovery mechanism must combine staleness with ownership and
additional safety checks before taking action.

## 3. Node Recovery Semantics

Future recovery should interpret persisted node states as follows:

- **Succeeded:** Preserve the result and do not execute the node again during a
  normal resume. Re-execution requires an explicit replay strategy and suitable
  idempotency guarantees.
- **Failed:** Preserve the failure as the outcome of the recorded attempt. A
  strategy may schedule another attempt only when policy permits it; recovery
  must not silently turn every failure into a retry.
- **Running:** Treat the outcome as indeterminate after the owning process is
  considered lost. Reconcile the external operation when possible. Otherwise,
  retry only when the node declares or provides an idempotent execution path;
  unsafe cases require operator intervention or terminal failure.
- **Pending:** Treat the node as not yet started. It may execute after recovery
  only when all dependency and workflow-definition checks succeed.

These semantics describe future decisions. They do not change current node
execution or status-transition behavior.

## 4. Required Future Capabilities

### Workflow definition versioning

Every execution must be associated with an immutable workflow definition
version or equivalent content identity. Recovery must use the same graph and
node configuration that created the execution, even if a newer workflow
definition has since been published.

### Idempotency for external side effects

Nodes that create external effects need a stable idempotency key, a provider
deduplication mechanism, or a reconciliation operation. The framework also
needs a way for node implementations to declare the recovery behavior they can
safely support.

### Execution history and audit trail

Latest-state snapshots are insufficient for diagnosing ambiguous transitions.
A future audit trail should record attempts and material state transitions with
timestamps, ownership, and failure information while keeping the current
aggregate available for efficient reads. This can be added as a history model
without requiring full event sourcing.

### Recovery worker

A future recovery worker may find stale executions, attempt an atomic ownership
claim, load the complete aggregate and matching workflow definition, evaluate a
configured recovery strategy, and persist the chosen outcome. It must avoid
executing work merely because an execution is stale or successfully claimed.

## 5. Recovery Decision Matrix

| Persisted observation or scenario | Uncertainty | Future action | Required safeguard |
| --- | --- | --- | --- |
| Running workflow has a fresh heartbeat | Owner is presumed active | Leave execution with its current owner | Heartbeat freshness policy |
| Running workflow has a stale or missing heartbeat | Owner may be stopped or temporarily unreachable | Treat as a candidate; claim before evaluating recovery | Atomic ownership claim and owner-loss policy |
| Node is `Succeeded` | Saved result is available | Preserve result and continue with eligible downstream nodes | Matching workflow definition version |
| Node is `Failed` | Failure may or may not be retryable | Apply an explicit retry, fail, or intervention strategy | Failure classification and attempt history |
| Node is `Running` after owner loss | External effect may be absent, partial, or complete | Reconcile; retry only when safe; otherwise require intervention | Node recovery semantics and idempotency |
| Node is `Pending` | Node has not been recorded as started | Execute after dependencies are confirmed | Matching DAG and dependency state |
| External side effect completed but success was not saved | Snapshot understates completed work | Reconcile by idempotency key or external operation identifier | Provider deduplication or reconciliation API |
| Process crashed before a state update was persisted | Latest transition may be missing | Use history and node semantics to select resume, retry, or intervention | Durable audit trail and atomic state writes |
| Workflow is completed | No recovery work is expected | Do not claim or resume it | Terminal-status guard |

## Future Boundary

Recovery should remain an orchestration concern outside node implementations.
Nodes provide behavior and, in the future, recovery-related guarantees;
persistence stores snapshots and history; a recovery strategy makes decisions;
and a worker applies those decisions. This separation keeps detection,
coordination, decision-making, and execution independently testable.
