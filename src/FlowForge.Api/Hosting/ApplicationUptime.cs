namespace FlowForge.Api.Hosting;

/// <summary>
/// Tracks elapsed time since the host registered its runtime services.
/// </summary>
public sealed class ApplicationUptime
{
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _startedAt;

    /// <summary>
    /// Initializes application uptime tracking.
    /// </summary>
    public ApplicationUptime(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _startedAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Gets elapsed host uptime.
    /// </summary>
    public TimeSpan GetUptime()
    {
        var uptime = _timeProvider.GetUtcNow() - _startedAt;
        return uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
    }
}
