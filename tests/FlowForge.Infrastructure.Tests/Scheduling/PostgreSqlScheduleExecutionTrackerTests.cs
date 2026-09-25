using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.PostgreSql;

namespace FlowForge.Infrastructure.Tests.Scheduling;

public sealed class PostgreSqlScheduleExecutionTrackerTests : PostgreSqlIntegrationTestBase
{
    [SkippableFact]
    public async Task TryTrackAsync_DuplicateOccurrence_ReturnsFalse()
    {
        var tracker = new PostgreSqlScheduleExecutionTracker(CreateConnectionFactory());
        var scheduleId = new WorkflowScheduleId(Guid.NewGuid());
        var occurrence = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        var first = await tracker.TryTrackAsync(
            scheduleId,
            occurrence,
            CancellationToken.None);
        var duplicate = await tracker.TryTrackAsync(
            scheduleId,
            occurrence,
            CancellationToken.None);

        Assert.True(first);
        Assert.False(duplicate);
    }

    [SkippableFact]
    public async Task TryTrackAsync_DifferentOccurrences_AreTrackedIndependently()
    {
        var tracker = new PostgreSqlScheduleExecutionTracker(CreateConnectionFactory());
        var scheduleId = new WorkflowScheduleId(Guid.NewGuid());
        var firstOccurrence = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var secondOccurrence = firstOccurrence.AddMinutes(5);

        var first = await tracker.TryTrackAsync(
            scheduleId,
            firstOccurrence,
            CancellationToken.None);
        var second = await tracker.TryTrackAsync(
            scheduleId,
            secondOccurrence,
            CancellationToken.None);
        var latest = await tracker.GetLastTrackedOccurrenceAsync(
            scheduleId,
            CancellationToken.None);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(secondOccurrence, latest);
    }
}
