using System.Runtime.ExceptionServices;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Provides the exception-classification boundary around node pipeline execution.
/// </summary>
internal sealed class NodeExecutionCoordinator(ExecutionPipeline executionPipeline)
{
    private const string UnexpectedExecutionFailureMessage =
        "Node execution failed because of an unexpected execution error.";

    /// <summary>
    /// Executes a node pipeline and converts only unexpected execution exceptions
    /// into classified node failures.
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        Func<NodeExecutionContext, CancellationToken, Task<NodeExecutionResult>> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        try
        {
            return await executionPipeline.ExecuteAsync(
                context,
                terminal,
                cancellationToken);
        }
        catch (NodeExecutionPersistenceException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new NodeExecutionResult
            {
                Success = false,
                Output = null,
                Failure = new NodeFailure
                {
                    Category = NodeFailureCategory.Execution,
                    Message = UnexpectedExecutionFailureMessage
                }
            };
        }
    }
}

/// <summary>
/// Marks an infrastructure exception raised by persistence performed from inside
/// the execution pipeline so the coordinator can propagate the original failure.
/// </summary>
internal sealed class NodeExecutionPersistenceException(Exception innerException)
    : Exception("Node execution persistence failed.", innerException);
