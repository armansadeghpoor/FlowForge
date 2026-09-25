namespace FlowForge.Abstractions.Security;

/// <summary>
/// Defines stable FlowForge permission identifiers.
/// </summary>
public static class Permissions
{
    /// <summary>Allows reading workflow definitions.</summary>
    public const string WorkflowDefinitionsRead = "workflow-definitions.read";

    /// <summary>Allows creating workflow definition versions.</summary>
    public const string WorkflowDefinitionsWrite = "workflow-definitions.write";

    /// <summary>Allows reading workflow triggers.</summary>
    public const string WorkflowTriggersRead = "workflow-triggers.read";

    /// <summary>Allows creating workflow triggers.</summary>
    public const string WorkflowTriggersWrite = "workflow-triggers.write";

    /// <summary>Allows executing workflow triggers.</summary>
    public const string WorkflowTriggersExecute = "workflow-triggers.execute";

    /// <summary>Allows reading workflow executions and timelines.</summary>
    public const string WorkflowExecutionsRead = "workflow-executions.read";

    /// <summary>Allows dispatching workflow events.</summary>
    public const string WorkflowEventsDispatch = "workflow-events.dispatch";

    /// <summary>Allows reading runtime diagnostics.</summary>
    public const string RuntimeDiagnosticsRead = "runtime-diagnostics.read";
}
