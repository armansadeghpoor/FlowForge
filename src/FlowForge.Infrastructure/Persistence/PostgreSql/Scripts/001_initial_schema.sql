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
    definition_version TEXT NOT NULL,
    status VARCHAR(32),
    started_at TIMESTAMP NULL,
    completed_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL,
    owner_id TEXT NULL,
    last_heartbeat_at TIMESTAMP NULL
);

CREATE TABLE node_executions
(
    id UUID PRIMARY KEY,
    workflow_execution_id UUID NOT NULL,
    node_id UUID NOT NULL,
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
