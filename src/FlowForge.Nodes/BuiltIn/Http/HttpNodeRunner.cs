using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Nodes.BuiltIn.Http;

/// <summary>
/// Executes workflow nodes that send HTTP requests.
/// </summary>
public sealed class HttpNodeRunner : INodeRunner
{
    private const int DefaultTimeoutMilliseconds = 30_000;

    private static readonly NodeDescriptor HttpDescriptor = new()
    {
        Type = "http",
        Version = "1.0",
        ConfigurationSchema = new ReadOnlyDictionary<string, NodePropertyDefinition>(
            new Dictionary<string, NodePropertyDefinition>(StringComparer.Ordinal)
            {
                ["method"] = new NodePropertyDefinition
                {
                    Name = "method",
                    Type = NodePropertyType.String,
                    Required = true
                },
                ["url"] = new NodePropertyDefinition
                {
                    Name = "url",
                    Type = NodePropertyType.String,
                    Required = true
                },
                ["headers"] = new NodePropertyDefinition
                {
                    Name = "headers",
                    Type = NodePropertyType.Object,
                    Required = false
                },
                ["body"] = new NodePropertyDefinition
                {
                    Name = "body",
                    Type = NodePropertyType.String,
                    Required = false
                },
                ["timeoutMs"] = new NodePropertyDefinition
                {
                    Name = "timeoutMs",
                    Type = NodePropertyType.Integer,
                    Required = false
                }
            })
    };

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new HTTP node runner.
    /// </summary>
    /// <param name="httpClient">The client used to send HTTP requests.</param>
    public HttpNodeRunner(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string NodeType => Descriptor.Type;

    /// <inheritdoc />
    public NodeDescriptor Descriptor => HttpDescriptor;

    /// <inheritdoc />
    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TryGetRequiredString(context.Node.Configuration, "method", out var methodName))
        {
            return Failure("HTTP node configuration 'method' must be a string.");
        }

        if (!TryGetHttpMethod(methodName, out var method))
        {
            return Failure(
                "HTTP node configuration 'method' must be one of GET, POST, PUT, DELETE, or PATCH.");
        }

        if (!TryGetRequiredString(context.Node.Configuration, "url", out var urlValue) ||
            !Uri.TryCreate(urlValue, UriKind.Absolute, out var uri) ||
            !IsHttpScheme(uri) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return Failure(
                "HTTP node configuration 'url' must be an absolute HTTP or HTTPS URI.");
        }

        if (!TryGetHeaders(context.Node.Configuration, out var headers, out var headersError))
        {
            return Failure(headersError);
        }

        if (!TryGetOptionalString(context.Node.Configuration, "body", out var body))
        {
            return Failure("HTTP node configuration 'body' must be a string when supplied.");
        }

        if (!TryGetTimeout(context.Node.Configuration, out var timeoutMilliseconds))
        {
            return Failure(
                "HTTP node configuration 'timeoutMs' must be a JSON integer greater than zero.");
        }

        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
        }

        foreach (var header in headers)
        {
            try
            {
                if (request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    continue;
                }

                if (request.Content is null)
                {
                    return Failure(
                        $"HTTP node configuration header '{header.Key}' cannot be applied to the request.");
                }

                request.Content.Headers.Remove(header.Key);
                if (!request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    return Failure(
                        $"HTTP node configuration header '{header.Key}' cannot be applied to the request.");
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return Failure(
                    $"HTTP node configuration header name '{header.Key}' is invalid.");
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeoutMilliseconds);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
        var output = new HttpResponseOutput
        {
            StatusCode = (int)response.StatusCode,
            Headers = ReadHeaders(response),
            Body = responseBody
        };

        if (!response.IsSuccessStatusCode)
        {
            return new NodeExecutionResult
            {
                Success = false,
                Output = new NodeOutput { Value = output },
                ErrorMessage =
                    $"HTTP request failed with status code {(int)response.StatusCode} ({response.ReasonPhrase})."
            };
        }

        return new NodeExecutionResult
        {
            Success = true,
            Output = new NodeOutput { Value = output },
            ErrorMessage = null
        };
    }

    private static bool TryGetRequiredString(
        IReadOnlyDictionary<string, JsonElement> configuration,
        string propertyName,
        out string value)
    {
        if (configuration.TryGetValue(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String &&
            element.GetString() is { } stringValue)
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetOptionalString(
        IReadOnlyDictionary<string, JsonElement> configuration,
        string propertyName,
        out string? value)
    {
        if (!configuration.TryGetValue(propertyName, out var element))
        {
            value = null;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetHttpMethod(string value, out HttpMethod method)
    {
        method = value.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            _ => null!
        };

        return method is not null;
    }

    private static bool IsHttpScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetHeaders(
        IReadOnlyDictionary<string, JsonElement> configuration,
        out IReadOnlyDictionary<string, string> headers,
        out string errorMessage)
    {
        var parsedHeaders = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!configuration.TryGetValue("headers", out var headersElement))
        {
            headers = parsedHeaders;
            errorMessage = string.Empty;
            return true;
        }

        if (headersElement.ValueKind != JsonValueKind.Object)
        {
            headers = parsedHeaders;
            errorMessage =
                "HTTP node configuration 'headers' must be a JSON object with string values.";
            return false;
        }

        foreach (var property in headersElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String ||
                property.Value.GetString() is not { } headerValue)
            {
                headers = parsedHeaders;
                errorMessage =
                    $"HTTP node configuration header '{property.Name}' must have a string value.";
                return false;
            }

            if (!parsedHeaders.TryAdd(property.Name, headerValue))
            {
                headers = parsedHeaders;
                errorMessage =
                    $"HTTP node configuration contains duplicate header '{property.Name}'.";
                return false;
            }
        }

        headers = parsedHeaders;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryGetTimeout(
        IReadOnlyDictionary<string, JsonElement> configuration,
        out int timeoutMilliseconds)
    {
        if (!configuration.TryGetValue("timeoutMs", out var timeoutElement))
        {
            timeoutMilliseconds = DefaultTimeoutMilliseconds;
            return true;
        }

        timeoutMilliseconds = 0;
        return timeoutElement.ValueKind == JsonValueKind.Number &&
            timeoutElement.TryGetInt32(out timeoutMilliseconds) &&
            timeoutMilliseconds > 0;
    }

    private static IReadOnlyDictionary<string, string[]> ReadHeaders(
        HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = header.Value.ToArray();
        }

        return new ReadOnlyDictionary<string, string[]>(headers);
    }

    private static NodeExecutionResult Failure(string errorMessage) =>
        new()
        {
            Success = false,
            Output = null,
            ErrorMessage = errorMessage
        };
}
