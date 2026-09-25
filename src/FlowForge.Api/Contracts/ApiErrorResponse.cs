namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents an API error response.
/// </summary>
public sealed record ApiErrorResponse
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the client-safe error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the identifier correlating the response with its HTTP request.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the complete set of returned errors when more detail is available.
    /// </summary>
    public required IReadOnlyList<ApiErrorDto> Errors { get; init; }
}

/// <summary>
/// Represents one API-safe error.
/// </summary>
public sealed record ApiErrorDto
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the client-safe error message.
    /// </summary>
    public required string Message { get; init; }
}
