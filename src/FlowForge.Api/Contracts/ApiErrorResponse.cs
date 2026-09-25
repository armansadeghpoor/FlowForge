namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents an API error response.
/// </summary>
public sealed record ApiErrorResponse
{
    /// <summary>
    /// Gets the returned errors.
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
