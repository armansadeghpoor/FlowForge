using Microsoft.AspNetCore.Diagnostics;

namespace FlowForge.Api.Errors;

/// <summary>
/// Converts unhandled API exceptions into a safe, stable error response.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            ApiErrorResponseFactory.Create(
                httpContext,
                "InternalServerError",
                "An unexpected error occurred."),
            cancellationToken);

        return true;
    }
}
