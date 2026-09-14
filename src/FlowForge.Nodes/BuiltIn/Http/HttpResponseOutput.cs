namespace FlowForge.Nodes.BuiltIn.Http;

/// <summary>
/// Represents the response produced by an HTTP node execution.
/// </summary>
public sealed record HttpResponseOutput
{
    /// <summary>
    /// Gets the numeric HTTP status code.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    public required IReadOnlyDictionary<string, string[]> Headers { get; init; }

    /// <summary>
    /// Gets the response body.
    /// </summary>
    public required string Body { get; init; }
}
