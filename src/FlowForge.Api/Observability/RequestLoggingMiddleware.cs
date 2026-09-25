using System.Diagnostics;
using FlowForge.Abstractions.Observability;
using FlowForge.Api.Correlation;

namespace FlowForge.Api.Observability;

/// <summary>
/// Logs HTTP request completion and records process-local request metrics.
/// </summary>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    IMetricsCollector metrics)
{
    /// <summary>Gets the total HTTP request counter name.</summary>
    public const string RequestCountMetric = "http.server.requests";

    /// <summary>Gets the HTTP request duration metric name.</summary>
    public const string RequestDurationMetric = "http.server.request.duration";

    /// <summary>
    /// Logs and measures the current request lifecycle.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

            metrics.IncrementCounter(RequestCountMetric);
            metrics.IncrementCounter(
                $"http.server.responses.{context.Response.StatusCode}");
            metrics.RecordDuration(RequestDurationMetric, duration);

            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {DurationMilliseconds} ms " +
                "with correlation {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Response.StatusCode,
                duration.TotalMilliseconds,
                correlationId);
        }
    }
}
