using Microsoft.AspNetCore.Diagnostics;

namespace FlowForge.Api.Errors;

internal static class ApiStatusCodeResponseWriter
{
    public static Task WriteAsync(StatusCodeContext context)
    {
        var (code, message) = context.HttpContext.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest =>
                ("BadRequest", "The request could not be processed."),
            StatusCodes.Status404NotFound =>
                ("ResourceNotFound", "The requested resource was not found."),
            StatusCodes.Status405MethodNotAllowed =>
                ("MethodNotAllowed", "The HTTP method is not supported for this resource."),
            _ => ("HttpError", "The request could not be completed.")
        };

        return context.HttpContext.Response.WriteAsJsonAsync(
            ApiErrorResponseFactory.Create(context.HttpContext, code, message));
    }
}
