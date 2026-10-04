using FlowForge.Core.Domain.Enums;

namespace FlowForge.Core.Tests.Execution;

public sealed class ExecutionLifecycleSemanticsTests
{
    [Fact]
    public void TerminalStatuses_IncludeSucceededFailedAndCancelled()
    {
        Assert.Equal(
            [
                WorkflowExecutionStatus.Succeeded,
                WorkflowExecutionStatus.Failed,
                WorkflowExecutionStatus.Cancelled
            ],
            Enum.GetValues<WorkflowExecutionStatus>()
                .Where(status => status is
                    WorkflowExecutionStatus.Succeeded or
                    WorkflowExecutionStatus.Failed or
                    WorkflowExecutionStatus.Cancelled));
        Assert.Equal(
            [
                NodeExecutionStatus.Succeeded,
                NodeExecutionStatus.Failed,
                NodeExecutionStatus.Cancelled
            ],
            Enum.GetValues<NodeExecutionStatus>()
                .Where(status => status is
                    NodeExecutionStatus.Succeeded or
                    NodeExecutionStatus.Failed or
                    NodeExecutionStatus.Cancelled));
    }

    [Fact]
    public void FailureCategories_KeepExecutionAndCancellationDistinct()
    {
        Assert.NotEqual(
            NodeFailureCategory.Execution,
            NodeFailureCategory.Cancelled);
    }

    [Fact]
    public void CancellationHistoryEvents_AreAppendedWithoutRenumberingExistingEvents()
    {
        Assert.Equal(0, (int)ExecutionHistoryEventType.WorkflowCreated);
        Assert.Equal(1, (int)ExecutionHistoryEventType.WorkflowStarted);
        Assert.Equal(2, (int)ExecutionHistoryEventType.WorkflowCompleted);
        Assert.Equal(3, (int)ExecutionHistoryEventType.WorkflowFailed);
        Assert.Equal(4, (int)ExecutionHistoryEventType.NodeStarted);
        Assert.Equal(5, (int)ExecutionHistoryEventType.NodeCompleted);
        Assert.Equal(6, (int)ExecutionHistoryEventType.NodeFailed);
        Assert.Equal(7, (int)ExecutionHistoryEventType.WorkflowCancelled);
        Assert.Equal(8, (int)ExecutionHistoryEventType.NodeCancelled);
    }
}
