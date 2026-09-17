using System.Net;
using System.Text;
using System.Text.Json;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Nodes.BuiltIn.Http;

namespace FlowForge.Nodes.Tests.BuiltIn.Http;

public sealed class HttpNodeRunnerTests
{
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpNodeRunner(null!));
    }

    [Fact]
    public void Descriptor_ExposesHttpContract()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler());
        var runner = new HttpNodeRunner(httpClient);

        Assert.Equal("http", runner.NodeType);
        Assert.Equal("http", runner.Descriptor.Type);
        Assert.Equal("1.0", runner.Descriptor.Version);
        Assert.Equal(5, runner.Descriptor.ConfigurationSchema.Count);
        AssertProperty(runner.Descriptor, "method", NodePropertyType.String, true);
        AssertProperty(runner.Descriptor, "url", NodePropertyType.String, true);
        AssertProperty(runner.Descriptor, "headers", NodePropertyType.Object, false);
        AssertProperty(runner.Descriptor, "body", NodePropertyType.String, false);
        AssertProperty(runner.Descriptor, "timeoutMs", NodePropertyType.Integer, false);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulGet_ReturnsResponseOutput()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("response body", Encoding.UTF8, "text/plain")
        };
        response.Headers.TryAddWithoutValidation("X-Result", ["first", "second"]);
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(response)));
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(ValidContext(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        var output = Assert.IsType<HttpResponseOutput>(result.Output?.Value);
        Assert.Equal(200, output.StatusCode);
        Assert.Equal("response body", output.Body);
        Assert.Equal(["first", "second"], output.Headers["X-Result"]);
        Assert.Contains("Content-Type", output.Headers.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Post_SendsConfiguredBody()
    {
        HttpMethod? sentMethod = null;
        string? sentBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, token) =>
        {
            sentMethod = request.Method;
            sentBody = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(string.Empty)
            };
        }));
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(
            ValidContext(method: "post", body: "request body"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, sentMethod);
        Assert.Equal("request body", sentBody);
    }

    [Fact]
    public async Task ExecuteAsync_Headers_SendsConfiguredValues()
    {
        string? sentHeader = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            sentHeader = Assert.Single(request.Headers.GetValues("X-FlowForge"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            });
        }));
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(
            ValidContext(headers: new Dictionary<string, string>
            {
                ["X-FlowForge"] = "test-value"
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("test-value", sentHeader);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.test/resource")]
    public async Task ExecuteAsync_InvalidUrl_ReturnsFailure(string url)
    {
        var handler = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(ValidContext(url: url), CancellationToken.None);

        AssertValidationFailure(result, "absolute HTTP or HTTPS");
        Assert.Equal(0, handler.InvocationCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedMethod_ReturnsFailure()
    {
        var handler = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(
            ValidContext(method: "OPTIONS"),
            CancellationToken.None);

        AssertValidationFailure(result, "GET, POST, PUT, DELETE, or PATCH");
        Assert.Equal(0, handler.InvocationCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidTimeout_ReturnsFailure(int timeoutMilliseconds)
    {
        var handler = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(
            ValidContext(timeoutMilliseconds: timeoutMilliseconds),
            CancellationToken.None);

        AssertValidationFailure(result, "timeoutMs");
        Assert.Equal(0, handler.InvocationCount);
    }

    [Fact]
    public async Task ExecuteAsync_NonSuccessStatus_ReturnsFailure()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream failed")
            })));
        var runner = new HttpNodeRunner(httpClient);

        var result = await runner.ExecuteAsync(ValidContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(NodeFailureCategory.External, result.Failure.Category);
        Assert.Contains("502", result.Failure.Message);
        var output = Assert.IsType<HttpResponseOutput>(result.Output?.Value);
        Assert.Equal(502, output.StatusCode);
        Assert.Equal("upstream failed", output.Body);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_PropagatesOperationCanceledException()
    {
        var handlerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (_, token) =>
        {
            handlerEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var runner = new HttpNodeRunner(httpClient);
        using var cancellationSource = new CancellationTokenSource();

        var executionTask = runner.ExecuteAsync(ValidContext(), cancellationSource.Token);
        await handlerEntered.Task;
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
    }

    private static void AssertProperty(
        NodeDescriptor descriptor,
        string name,
        NodePropertyType type,
        bool required)
    {
        var property = descriptor.ConfigurationSchema[name];
        Assert.Equal(name, property.Name);
        Assert.Equal(type, property.Type);
        Assert.Equal(required, property.Required);
    }

    private static void AssertValidationFailure(
        NodeExecutionResult result,
        string expectedMessagePart)
    {
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.NotNull(result.Failure);
        Assert.Equal(NodeFailureCategory.Validation, result.Failure.Category);
        Assert.Contains(expectedMessagePart, result.Failure.Message);
    }

    private static NodeExecutionContext ValidContext(
        string method = "GET",
        string url = "https://example.test/resource",
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        int? timeoutMilliseconds = null)
    {
        var configuration = new Dictionary<string, JsonElement>
        {
            ["method"] = JsonSerializer.SerializeToElement(method),
            ["url"] = JsonSerializer.SerializeToElement(url)
        };

        if (headers is not null)
        {
            configuration["headers"] = JsonSerializer.SerializeToElement(headers);
        }

        if (body is not null)
        {
            configuration["body"] = JsonSerializer.SerializeToElement(body);
        }

        if (timeoutMilliseconds is not null)
        {
            configuration["timeoutMs"] = JsonSerializer.SerializeToElement(timeoutMilliseconds.Value);
        }

        return new NodeExecutionContext
        {
            Node = new NodeDefinition
            {
                Id = new NodeId(Guid.NewGuid()),
                Type = "http",
                Configuration = configuration
            },
            ExecutionId = new WorkflowExecutionId(Guid.NewGuid())
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? send = null)
        : HttpMessageHandler
    {
        private int _invocationCount;

        public int InvocationCount => Volatile.Read(ref _invocationCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);
            return send?.Invoke(request, cancellationToken) ??
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
