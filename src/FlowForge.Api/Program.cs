using FlowForge.Application.Definitions;
using FlowForge.Application.Diagnostics;
using FlowForge.Application.Events;
using FlowForge.Application.Executions;
using FlowForge.Application.Queries;
using FlowForge.Application.Schedules;
using FlowForge.Application.Security;
using FlowForge.Application.Triggers;
using FlowForge.Api.Configuration;
using FlowForge.Api.Hosting;
using FlowForge.Api.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowForgeApi();
builder.Services.AddFlowForgeConfiguration(builder.Configuration);
builder.Services.AddFlowForgeAuthentication(builder.Configuration);
builder.Services.AddFlowForgeHosting(builder.Environment);
builder.Services.AddFlowForgeObservability();
builder.Services.AddScoped<IRuntimeDiagnosticsService, RuntimeDiagnosticsService>();
builder.Services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
builder.Services.AddScoped<IWorkflowEventService, WorkflowEventService>();
builder.Services.AddScoped<IWorkflowExecutionCommandService, WorkflowExecutionCommandService>();
builder.Services.AddScoped<IWorkflowTriggerService, WorkflowTriggerService>();
builder.Services.AddScoped<IWorkflowScheduleService, WorkflowScheduleService>();
builder.Services.AddScoped<IWorkflowEventTriggerService, WorkflowEventTriggerService>();
builder.Services.AddScoped<IWorkflowExecutionQueryService, WorkflowExecutionQueryService>();
builder.Services.AddScoped<
    FlowForge.Abstractions.Security.IAuthorizationService,
    PermissionAuthorizationService>();

var app = builder.Build();

app.UseMiddleware<FlowForge.Api.Correlation.CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(FlowForge.Api.Errors.ApiStatusCodeResponseWriter.WriteAsync);
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<FlowForge.Api.Security.SecurityContextMiddleware>();
app.UseMiddleware<FlowForge.Api.Tenancy.TenantContextMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Exposes the API entry point for hosting and tests.
/// </summary>
public partial class Program;
