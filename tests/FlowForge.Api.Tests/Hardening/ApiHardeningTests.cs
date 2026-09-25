using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using FlowForge.Api.Configuration;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Api.Correlation;
using FlowForge.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowForge.Api.Tests.Hardening;

public sealed class ApiHardeningTests
{
    [Fact]
    public async Task ExceptionHandler_ReturnsStandardSafeErrorResponse()
    {
        const string correlationId = "request-correlation";
        var context = CreateHttpContext();
        context.TraceIdentifier = correlationId;
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("sensitive implementation detail"),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.NotNull(response);
        Assert.Equal("InternalServerError", response.Code);
        Assert.Equal("An unexpected error occurred.", response.Message);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.DoesNotContain("sensitive implementation detail", response.Message);
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesExistingHeader()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.TraceIdentifier);
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task CorrelationMiddleware_GeneratesMissingHeader()
    {
        var context = CreateHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var responseValue = context.Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString();
        Assert.True(Guid.TryParse(responseValue, out _));
        Assert.Equal(responseValue, context.TraceIdentifier);
    }

    [Fact]
    public void InvalidModelState_ProducesStandardBadRequestBeforeActionExecution()
    {
        const string correlationId = "validation-correlation";
        var services = new ServiceCollection();
        services.AddFlowForgeApi();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var httpContext = CreateHttpContext();
        httpContext.TraceIdentifier = correlationId;
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.ModelState.AddModelError("eventType", "The field is required.");

        var result = options.InvalidModelStateResponseFactory(actionContext);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.False(options.SuppressModelStateInvalidFilter);
        Assert.Equal("RequestValidationFailed", response.Code);
        Assert.Equal(correlationId, response.CorrelationId);
    }

    [Fact]
    public void RequestContracts_RejectMissingRequiredValues()
    {
        var request = new DispatchWorkflowEventRequest
        {
            EventType = string.Empty,
            Payload = JsonSerializer.SerializeToElement(new { })
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(nameof(request.EventType)));
    }

    [Fact]
    public void Controllers_UseVersionedRoutes()
    {
        var routeTemplates = typeof(WorkflowDefinitionsController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.NotEmpty(routeTemplates);
        Assert.All(
            routeTemplates,
            template => Assert.StartsWith("api/v1/", template, StringComparison.Ordinal));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        return context;
    }
}
