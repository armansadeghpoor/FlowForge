using FlowForge.Application.Definitions;
using FlowForge.Application.Diagnostics;
using FlowForge.Application.Events;
using FlowForge.Application.Executions;
using FlowForge.Application.Queries;
using FlowForge.Application.Schedules;
using FlowForge.Application.Triggers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IRuntimeDiagnosticsService, RuntimeDiagnosticsService>();
builder.Services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
builder.Services.AddScoped<IWorkflowEventService, WorkflowEventService>();
builder.Services.AddScoped<IWorkflowExecutionCommandService, WorkflowExecutionCommandService>();
builder.Services.AddScoped<IWorkflowTriggerService, WorkflowTriggerService>();
builder.Services.AddScoped<IWorkflowScheduleService, WorkflowScheduleService>();
builder.Services.AddScoped<IWorkflowEventTriggerService, WorkflowEventTriggerService>();
builder.Services.AddScoped<IWorkflowExecutionQueryService, WorkflowExecutionQueryService>();

var app = builder.Build();

app.MapControllers();

app.Run();

/// <summary>
/// Exposes the API entry point for hosting and tests.
/// </summary>
public partial class Program;
