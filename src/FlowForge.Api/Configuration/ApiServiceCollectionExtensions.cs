using FlowForge.Abstractions.Security;
using FlowForge.Api.Errors;
using FlowForge.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Configuration;

/// <summary>
/// Registers the hardened HTTP API boundary.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds controllers, standardized model validation, and global exception handling.
    /// </summary>
    public static IServiceCollection AddFlowForgeApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                    new BadRequestObjectResult(
                        ApiErrorResponseFactory.Create(
                            context.HttpContext,
                            "RequestValidationFailed",
                            "The request is invalid."));
            });
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddScoped<HttpUserContext>();
        services.AddScoped<IUserContext>(serviceProvider =>
            serviceProvider.GetRequiredService<HttpUserContext>());

        return services;
    }
}
