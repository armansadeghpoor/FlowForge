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

    public static IActionResult ToActionResult(IReadOnlyList<ApplicationError> errors)
    {
        var response = new ApiErrorResponse
        {
            Errors = Array.AsReadOnly(errors
                .Select(error => new ApiErrorDto
                {
                    Code = error.Code,
                    Message = error.Message
                })
                .ToArray())
        };

        if (errors.Any(error => error.Code.EndsWith("NotFound", StringComparison.Ordinal)))
        {
            return new NotFoundObjectResult(response);
        }

        if (errors.Any(error => error.Code.EndsWith("AlreadyExists", StringComparison.Ordinal)))
        {
            return new ConflictObjectResult(response);
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
