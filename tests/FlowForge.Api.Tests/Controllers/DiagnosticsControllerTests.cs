using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Application.Common;
using FlowForge.Application.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Tests.Controllers;

public sealed class DiagnosticsControllerTests
{
    [Fact]
    public async Task GetExecutionMetricsAsync_ReturnsMetricsDto()
    {
        var timestamp = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var service = new RuntimeDiagnosticsServiceStub(
            new ExecutionMetricsSnapshot
            {
                TotalExecutions = 5,
                RunningExecutions = 1,
                CompletedExecutions = 3,
                FailedExecutions = 1,
                AverageDuration = TimeSpan.FromSeconds(30),
                LastExecutionTimestamp = timestamp
            });

        var result = await new DiagnosticsController(service)
            .GetExecutionMetricsAsync(CancellationToken.None);

        var response = Assert.IsType<ExecutionMetricsDto>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(5, response.TotalExecutions);
        Assert.Equal(1, response.RunningExecutions);
        Assert.Equal(3, response.CompletedExecutions);
        Assert.Equal(1, response.FailedExecutions);
        Assert.Equal(TimeSpan.FromSeconds(30), response.AverageDuration);
        Assert.Equal(timestamp, response.LastExecutionTimestamp);
    }

    [Fact]
    public async Task GetExecutionMetricsAsync_EmptySnapshot_ReturnsZeroMetrics()
    {
        var service = new RuntimeDiagnosticsServiceStub(
            new ExecutionMetricsSnapshot
            {
                TotalExecutions = 0,
                RunningExecutions = 0,
                CompletedExecutions = 0,
                FailedExecutions = 0,
                AverageDuration = null,
                LastExecutionTimestamp = null
            });

        var result = await new DiagnosticsController(service)
            .GetExecutionMetricsAsync(CancellationToken.None);

        var response = Assert.IsType<ExecutionMetricsDto>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(0, response.TotalExecutions);
        Assert.Null(response.AverageDuration);
        Assert.Null(response.LastExecutionTimestamp);
    }

    private sealed class RuntimeDiagnosticsServiceStub(ExecutionMetricsSnapshot snapshot)
        : IRuntimeDiagnosticsService
    {
        public Task<ApplicationResult<ExecutionMetricsSnapshot>> GetExecutionMetricsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ApplicationResult<ExecutionMetricsSnapshot>.Success(snapshot));
    }
}
