using FlowForge.Api.Contracts;
using FlowForge.Api.Correlation;

namespace FlowForge.Api.Errors;

internal static class ApiErrorResponseFactory
{
    public static ApiErrorResponse Create(
        HttpContext? context,
        string code,
        string message,
        IReadOnlyList<ApiErrorDto>? errors = null)
    {
        var details = errors ??
        [
            new ApiErrorDto
            {
                Code = code,
                Message = message
            }
        ];

        return new ApiErrorResponse
        {
            Code = code,
            Message = message,
            CorrelationId = CorrelationIdMiddleware.GetCorrelationId(context),
            Errors = details
        };
    }
}
