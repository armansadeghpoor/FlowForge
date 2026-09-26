using FlowForge.Api.Contracts;
using FlowForge.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Errors;

internal static class ApplicationErrorMapper
{
    private static readonly HashSet<string> ValidationCodes = new(StringComparer.Ordinal)
    {
        "CycleDetected",
        "DuplicateEdge",
        "DuplicateNodeId",
        "EmptyWorkflow",
        "MissingFromNode",
        "MissingToNode",
        "RequiredNodePropertyMissing",
        "SelfReferencingEdge"
    };

    public static IActionResult ToActionResult(
        IReadOnlyList<ApplicationError> errors,
        HttpContext? httpContext)
    {
        var details = Array.AsReadOnly(errors
            .Select(error => new ApiErrorDto
            {
                Code = error.Code,
                Message = error.Message
            })
            .ToArray());
        var primaryError = errors.FirstOrDefault() ??
            new ApplicationError("Unexpected", "An unexpected error occurred.");
        var response = ApiErrorResponseFactory.Create(
            httpContext,
            primaryError.Code,
            primaryError.Message,
            details);

        if (errors.Any(error => error.Code.EndsWith("NotFound", StringComparison.Ordinal)))
        {
            return new NotFoundObjectResult(response);
        }

        if (errors.Any(error =>
                error.Code.EndsWith("AlreadyExists", StringComparison.Ordinal) ||
                error.Code.EndsWith("Conflict", StringComparison.Ordinal)))
        {
            return new ConflictObjectResult(response);
        }

        if (errors.Any(error =>
                error.Code.Equals("PermissionDenied", StringComparison.Ordinal)))
        {
            return new ObjectResult(response)
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        if (errors.Count > 0 && errors.All(IsValidationError))
        {
            return new BadRequestObjectResult(response);
        }

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }

    private static bool IsValidationError(ApplicationError error) =>
        error.Code.EndsWith("Required", StringComparison.Ordinal) ||
        error.Code.EndsWith("Invalid", StringComparison.Ordinal) ||
        ValidationCodes.Contains(error.Code);
}
