CREATE TABLE workflow_definitions
(
    id UUID NOT NULL,
    version TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NULL,
    definition JSONB NOT NULL,
    created_at TIMESTAMP NOT NULL,
    CONSTRAINT pk_workflow_definitions PRIMARY KEY (id, version)
);

CREATE TABLE workflow_executions
(
    id UUID PRIMARY KEY,
    workflow_id UUID NOT NULL,
    correlation_id UUID NOT NULL,
    definition_version TEXT NOT NULL,
    status VARCHAR(32),
    started_at TIMESTAMP NULL,
    completed_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL,
    owner_id TEXT NULL,
    last_heartbeat_at TIMESTAMP NULL
);

CREATE TABLE execution_requests
(
    id UUID PRIMARY KEY
);

CREATE TABLE schedule_executions
(
    schedule_id UUID NOT NULL,
    occurrence_at TIMESTAMP NOT NULL,
    CONSTRAINT pk_schedule_executions PRIMARY KEY (schedule_id, occurrence_at)
);

CREATE TABLE node_executions
(
    id UUID PRIMARY KEY,
    workflow_execution_id UUID NOT NULL,
    node_id UUID NOT NULL,
    correlation_id UUID NOT NULL,
    status VARCHAR(32),
    attempt_number INTEGER NOT NULL,
    output JSONB NULL,
    failure JSONB NULL,
    started_at TIMESTAMP NULL,
    completed_at TIMESTAMP NULL,
    CONSTRAINT fk_node_executions_workflow_executions
        FOREIGN KEY (workflow_execution_id)
        REFERENCES workflow_executions (id)
);

CREATE INDEX ix_node_executions_workflow_execution_id
    ON node_executions (workflow_execution_id);

CREATE INDEX ix_workflow_executions_correlation_id
    ON workflow_executions (correlation_id, created_at, id);

CREATE TABLE execution_history
(
    id UUID PRIMARY KEY,
    workflow_execution_id UUID NOT NULL,
    node_execution_id UUID NULL,
    event_type VARCHAR(32) NOT NULL,
    occurred_at TIMESTAMP NOT NULL,
    metadata JSONB NULL,
    append_sequence BIGINT GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT fk_execution_history_workflow_executions
        FOREIGN KEY (workflow_execution_id)
        REFERENCES workflow_executions (id)
);

CREATE INDEX ix_execution_history_workflow_execution_id
    ON execution_history (workflow_execution_id, occurred_at, append_sequence);

CREATE TABLE audit_entries
(
    id UUID PRIMARY KEY,
    user_id TEXT NULL,
    tenant_id UUID NULL,
    resource_tenant_id UUID NULL,
    action TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_identifier TEXT NOT NULL,
    outcome VARCHAR(32) NOT NULL,
    correlation_id UUID NOT NULL,
    occurred_at TIMESTAMP NOT NULL,
    metadata JSONB NULL,
    append_sequence BIGINT GENERATED ALWAYS AS IDENTITY
);

CREATE INDEX ix_audit_entries_tenant_id
    ON audit_entries (tenant_id);

CREATE INDEX ix_audit_entries_resource_tenant_id
    ON audit_entries (resource_tenant_id);

CREATE INDEX ix_audit_entries_correlation_id
    ON audit_entries (correlation_id);

CREATE INDEX ix_audit_entries_resource
    ON audit_entries (resource_type, resource_identifier);

CREATE INDEX ix_audit_entries_occurred_at
    ON audit_entries (occurred_at);

CREATE TABLE workflow_sharing
(
    id UUID PRIMARY KEY,
    workflow_definition_id UUID NOT NULL,
    definition_version TEXT NOT NULL,
    owner_tenant_id UUID NOT NULL,
    visibility VARCHAR(32) NOT NULL,
    shared_tenant_ids UUID[] NOT NULL,
    created_at TIMESTAMP NOT NULL,
    CONSTRAINT uq_workflow_sharing_definition_version
        UNIQUE (workflow_definition_id, definition_version)
);

CREATE INDEX ix_workflow_sharing_owner_tenant_id
    ON workflow_sharing (owner_tenant_id);

CREATE INDEX ix_workflow_sharing_shared_tenant_ids
    ON workflow_sharing USING GIN (shared_tenant_ids);
