using System.Collections.Concurrent;
using FlowForge.Abstractions.Observability;

namespace FlowForge.Api.Observability;

/// <summary>
/// Collects process-local operational metrics in memory.
/// </summary>
public sealed class InMemoryMetricsCollector : IMetricsCollector
{
    private readonly ConcurrentDictionary<string, long> _counters =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DurationAccumulator> _durations =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void IncrementCounter(string name, long amount = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        _counters.AddOrUpdate(name, amount, (_, current) => checked(current + amount));
    }

    /// <inheritdoc />
    public void RecordDuration(string name, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        _durations.GetOrAdd(name, _ => new DurationAccumulator()).Record(duration);
    }

    /// <inheritdoc />
    public MetricsSnapshot GetSnapshot() =>
        new(
            _counters,
            _durations.Select(pair =>
                new KeyValuePair<string, DurationMetricSnapshot>(
                    pair.Key,
                    pair.Value.GetSnapshot())));

    private sealed class DurationAccumulator
    {
        private readonly object _sync = new();
        private long _count;
        private long _totalTicks;

        public void Record(TimeSpan duration)
        {
            lock (_sync)
            {
                _count++;
                _totalTicks = checked(_totalTicks + duration.Ticks);
            }
        }

        public DurationMetricSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                var total = TimeSpan.FromTicks(_totalTicks);
                var average = _count == 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromTicks(_totalTicks / _count);
                return new DurationMetricSnapshot(_count, total, average);
            }
        }
    }
}
