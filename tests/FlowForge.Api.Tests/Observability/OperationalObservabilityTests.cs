using FlowForge.Abstractions.Hosting;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Api.Correlation;
using FlowForge.Api.Hosting;
using FlowForge.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FlowForge.Api.Tests.Observability;

public sealed class OperationalObservabilityTests
{
    [Fact]
    public void InMemoryMetricsCollector_CollectsCountersAndDurations()
    {
        var metrics = new InMemoryMetricsCollector();

        metrics.IncrementCounter("requests");
        metrics.IncrementCounter("requests", 2);
        metrics.RecordDuration("latency", TimeSpan.FromMilliseconds(10));
        metrics.RecordDuration("latency", TimeSpan.FromMilliseconds(20));

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(3, snapshot.Counters["requests"]);
        var duration = snapshot.Durations["latency"];
        Assert.Equal(2, duration.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(30), duration.Total);
        Assert.Equal(TimeSpan.FromMilliseconds(15), duration.Average);
    }

    [Fact]
    public async Task RequestLogging_RecordsSafeLifecycleDataAndCorrelation()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        var metrics = new InMemoryMetricsCollector();
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var requestLogging = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            },
            logger,
            metrics);
        var correlation = new CorrelationIdMiddleware(
            context => requestLogging.InvokeAsync(context));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/workflows";
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        httpContext.Request.Headers.Authorization = "Bearer sensitive-token";
        httpContext.Request.Body = new MemoryStream("sensitive-body"u8.ToArray());

        await correlation.InvokeAsync(httpContext);

        var message = Assert.Single(logger.Messages);
        Assert.Contains(HttpMethods.Post, message, StringComparison.Ordinal);
        Assert.Contains("/api/v1/workflows", message, StringComparison.Ordinal);
        Assert.Contains("202", message, StringComparison.Ordinal);
        Assert.Contains(correlationId, message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-token", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-body", message, StringComparison.Ordinal);
        Assert.Equal(
            correlationId,
            httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.Counters[RequestLoggingMiddleware.RequestCountMetric]);
        Assert.Equal(1, snapshot.Counters["http.server.responses.202"]);
        Assert.Equal(
            1,
            snapshot.Durations[RequestLoggingMiddleware.RequestDurationMetric].Count);
    }

    [Fact]
    public void RuntimeDiagnostics_ReturnsApplicationUptimeAndMetrics()
    {
        var timeProvider = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var uptime = new ApplicationUptime(timeProvider);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        var metrics = new InMemoryMetricsCollector();
        metrics.IncrementCounter(RequestLoggingMiddleware.RequestCountMetric, 4);
        metrics.RecordDuration(
            RequestLoggingMiddleware.RequestDurationMetric,
            TimeSpan.FromMilliseconds(25));
        var controller = new RuntimeDiagnosticsController(
            new ApplicationInformation("FlowForge.Api", "1.0.0", "Production"),
            uptime,
            metrics);

        var result = controller.Get();

        var response = Assert.IsType<RuntimeDiagnosticsDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("FlowForge.Api", response.ApplicationName);
        Assert.Equal("1.0.0", response.ApplicationVersion);
        Assert.Equal("Production", response.Environment);
        Assert.Equal(TimeSpan.FromMinutes(5), response.Uptime);
        Assert.Equal(
            4,
            response.Metrics.Counters[RequestLoggingMiddleware.RequestCountMetric]);
        Assert.Equal(
            TimeSpan.FromMilliseconds(25),
            response.Metrics.Durations[RequestLoggingMiddleware.RequestDurationMetric].Average);
    }

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan duration) => _current += duration;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
