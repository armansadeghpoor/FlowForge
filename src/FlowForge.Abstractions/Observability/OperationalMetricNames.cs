namespace FlowForge.Abstractions.Observability;

/// <summary>
/// Defines stable names for FlowForge operational metrics.
/// </summary>
public static class OperationalMetricNames
{
    /// <summary>Counts successfully created workflow definitions.</summary>
    public const string WorkflowDefinitionsCreated =
        "flowforge.workflow_definitions.created";

    /// <summary>Counts completed workflow definition read operations.</summary>
    public const string WorkflowDefinitionsRead =
        "flowforge.workflow_definitions.read";

    /// <summary>Counts completed workflow definition list operations.</summary>
    public const string WorkflowDefinitionsListed =
        "flowforge.workflow_definitions.listed";

    /// <summary>Counts workflow executions that entered the running state.</summary>
    public const string WorkflowExecutionsStarted =
        "flowforge.workflow_executions.started";

    /// <summary>Counts workflow executions that reached a terminal state.</summary>
    public const string WorkflowExecutionsCompleted =
        "flowforge.workflow_executions.completed";

    /// <summary>Counts successfully completed workflow executions.</summary>
    public const string WorkflowExecutionsSucceeded =
        "flowforge.workflow_executions.succeeded";

    /// <summary>Counts failed workflow executions.</summary>
    public const string WorkflowExecutionsFailed =
        "flowforge.workflow_executions.failed";

    /// <summary>Records terminal workflow execution durations.</summary>
    public const string WorkflowExecutionDuration =
        "flowforge.workflow_executions.duration";

    /// <summary>Counts allowed authorization decisions.</summary>
    public const string AuthorizationAllowed =
        "flowforge.authorization.allowed";

    /// <summary>Counts denied authorization decisions.</summary>
    public const string AuthorizationDenied =
        "flowforge.authorization.denied";

    /// <summary>Counts successfully recorded enterprise audit entries.</summary>
    public const string AuditEventsRecorded =
        "flowforge.audit.events.recorded";

    /// <summary>Counts enterprise audit entries that could not be recorded.</summary>
    public const string AuditEventsFailed =
        "flowforge.audit.events.failed";

    /// <summary>Counts completed HTTP requests.</summary>
    public const string HttpRequests = "http.server.requests";

    /// <summary>Records HTTP request durations.</summary>
    public const string HttpRequestDuration = "http.server.request.duration";

    /// <summary>Prefixes bounded HTTP response status-category counters.</summary>
    public const string HttpResponseCategoryPrefix = "http.server.responses";
}
