using FlowForge.Abstractions.Observability;

namespace FlowForge.Application.Tests.Observability;

internal sealed class RecordingMetricsCollector : IMetricsCollector
{
    private readonly Dictionary<string, long> _counters =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TimeSpan>> _durations =
        new(StringComparer.Ordinal);

    public void IncrementCounter(string name, long amount = 1) =>
        _counters[name] = _counters.GetValueOrDefault(name) + amount;

    public void RecordDuration(string name, TimeSpan duration)
    {
        if (!_durations.TryGetValue(name, out var observations))
        {
            observations = [];
            _durations.Add(name, observations);
        }

        observations.Add(duration);
    }

    public MetricsSnapshot GetSnapshot() =>
        new(
            _counters,
            _durations.Select(pair =>
            {
                var total = TimeSpan.FromTicks(
                    pair.Value.Sum(duration => duration.Ticks));
                return new KeyValuePair<string, DurationMetricSnapshot>(
                    pair.Key,
                    new DurationMetricSnapshot(
                        pair.Value.Count,
                        total,
                        TimeSpan.FromTicks(total.Ticks / pair.Value.Count)));
            }));
}
