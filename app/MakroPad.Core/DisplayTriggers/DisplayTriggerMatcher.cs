using MakroPad.Core.Configuration;

namespace MakroPad.Core.DisplayTriggers;

public static class DisplayTriggerMatcher
{
    public static bool ConditionMatches(DisplayTriggerConfig trigger, int percent, int previousPercent)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return DisplayTriggerNormalizer.NormalizeCondition(trigger.Condition) switch
        {
            "inRange" => percent >= trigger.MinPercent && percent <= trigger.MaxPercent,
            "increasing" => percent > previousPercent,
            "decreasing" => percent < previousPercent,
            _ => true
        };
    }

    public static bool ThresholdMatches(int changePercent, int percent, int previousPercent)
    {
        var threshold = DisplayTriggerNormalizer.NormalizeChangePercent(changePercent);
        var difference = Math.Abs(percent - previousPercent);

        return threshold == 0 ? difference > 0 : difference >= threshold;
    }

    public static bool ActionStatusMatches(DisplayTriggerConfig trigger, string? status)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var expected = DisplayTriggerNormalizer.NormalizeActionStatus(trigger.ActionStatus);
        return expected == "any" || expected == DisplayTriggerNormalizer.NormalizeActionStatus(status);
    }

    public static bool ConnectionStatusMatches(DisplayTriggerConfig trigger, string? status)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var expected = DisplayTriggerNormalizer.NormalizeConnectionStatus(trigger.ConnectionStatus);
        return expected == "any" || expected == DisplayTriggerNormalizer.NormalizeConnectionStatus(status);
    }
}
