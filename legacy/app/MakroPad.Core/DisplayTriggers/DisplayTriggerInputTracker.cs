using MakroPad.Core.Configuration;

namespace MakroPad.Core.DisplayTriggers;

public sealed class DisplayTriggerInputTracker
{
    private readonly Dictionary<string, int> lastTriggeredPercentByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();

    public bool ShouldTrigger(string key, DisplayTriggerConfig trigger, int percent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(trigger);

        lock (syncRoot)
        {
            var normalized = DisplayTriggerNormalizer.Normalize(trigger);
            var currentPercent = DisplayTriggerNormalizer.NormalizePercent(percent);
            var hasPrevious = lastTriggeredPercentByKey.TryGetValue(key, out var previousPercent);
            if (!hasPrevious)
            {
                previousPercent = currentPercent;
                lastTriggeredPercentByKey[key] = currentPercent;
            }

            var thresholdMatches = hasPrevious
                ? DisplayTriggerMatcher.ThresholdMatches(normalized.ChangePercent, currentPercent, previousPercent)
                : normalized.ChangePercent == 0;
            if (!thresholdMatches
                || !DisplayTriggerMatcher.ConditionMatches(normalized, currentPercent, previousPercent))
            {
                return false;
            }

            lastTriggeredPercentByKey[key] = currentPercent;
            return true;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            lastTriggeredPercentByKey.Clear();
        }
    }
}
