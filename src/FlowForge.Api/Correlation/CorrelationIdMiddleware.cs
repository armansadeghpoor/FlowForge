namespace FlowForge.Api.Correlation;

/// <summary>
/// Establishes and propagates an HTTP correlation identifier for each request.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Gets the HTTP header used for correlation identifiers.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "FlowForge.CorrelationId";

    /// <summary>
    /// Applies correlation handling to the current request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.TraceIdentifier = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }

    internal static string GetCorrelationId(HttpContext? context)
    {
        if (context?.Items[ItemKey] is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        var headerValue = context?.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        if (!string.IsNullOrWhiteSpace(context?.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        return Guid.NewGuid().ToString("D");
    }

    internal static Guid GetExecutionCorrelationId(HttpContext? context) =>
        Guid.TryParse(GetCorrelationId(context), out var correlationId)
            ? correlationId
            : Guid.NewGuid();
}
