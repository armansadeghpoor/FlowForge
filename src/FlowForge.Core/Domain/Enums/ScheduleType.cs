namespace FlowForge.Core.Domain.Enums;

/// <summary>
/// Identifies the expression semantics of a workflow schedule.
/// </summary>
public enum ScheduleType
{
    Cron,
    Interval,
    OneTime
}
