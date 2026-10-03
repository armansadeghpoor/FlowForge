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
    public const string RequestCountMetric = OperationalMetricNames.HttpRequests;

    /// <summary>Gets the HTTP request duration metric name.</summary>
    public const string RequestDurationMetric = OperationalMetricNames.HttpRequestDuration;

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
            metrics.IncrementCounter(
                $"{OperationalMetricNames.HttpResponseCategoryPrefix}." +
                GetStatusCategory(context.Response.StatusCode));
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

    private static string GetStatusCategory(int statusCode) =>
        statusCode is >= 100 and <= 599
            ? $"{statusCode / 100}xx"
            : "other";
}
