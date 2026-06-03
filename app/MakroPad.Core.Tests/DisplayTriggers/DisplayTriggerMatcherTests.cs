using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Tests.DisplayTriggers;

public sealed class DisplayTriggerMatcherTests
{
    [Theory]
    [InlineData("any", 50, 50, true)]
    [InlineData("unknown", 50, 50, true)]
    [InlineData("inRange", 20, 10, true)]
    [InlineData("inRange", 80, 90, true)]
    [InlineData("inRange", 19, 10, false)]
    [InlineData("inRange", 81, 90, false)]
    [InlineData("increasing", 51, 50, true)]
    [InlineData("increasing", 50, 50, false)]
    [InlineData("decreasing", 49, 50, true)]
    [InlineData("decreasing", 50, 50, false)]
    public void ConditionMatchesExpectedBehavior(
        string condition,
        int percent,
        int previousPercent,
        bool expected)
    {
        var trigger = new DisplayTriggerConfig
        {
            Condition = condition,
            MinPercent = 20,
            MaxPercent = 80
        };

        Assert.Equal(expected, DisplayTriggerMatcher.ConditionMatches(trigger, percent, previousPercent));
    }

    [Theory]
    [InlineData(0, 50, 50, false)]
    [InlineData(0, 51, 50, true)]
    [InlineData(5, 54, 50, false)]
    [InlineData(5, 55, 50, true)]
    [InlineData(5, 45, 50, true)]
    [InlineData(-1, 49, 50, true)]
    [InlineData(101, 0, 100, true)]
    public void ThresholdMatchesNormalizedAbsoluteDifference(
        int changePercent,
        int percent,
        int previousPercent,
        bool expected)
    {
        Assert.Equal(expected, DisplayTriggerMatcher.ThresholdMatches(changePercent, percent, previousPercent));
    }

    [Theory]
    [InlineData("any", "ok", true)]
    [InlineData("ok", "ok", true)]
    [InlineData("failed", "ok", false)]
    [InlineData("OK", "ok", true)]
    [InlineData("ok", "OK", true)]
    public void ActionStatusMatchesNormalizedStatus(string configured, string actual, bool expected)
    {
        var trigger = new DisplayTriggerConfig { ActionStatus = configured };

        Assert.Equal(expected, DisplayTriggerMatcher.ActionStatusMatches(trigger, actual));
    }

    [Theory]
    [InlineData("any", "connected", true)]
    [InlineData("CONNECTED", "connected", true)]
    [InlineData("disconnected", "CONNECTED", false)]
    [InlineData("connected", "unknown", false)]
    public void ConnectionStatusMatchesNormalizedStatus(string configured, string actual, bool expected)
    {
        var trigger = new DisplayTriggerConfig { ConnectionStatus = configured };

        Assert.Equal(expected, DisplayTriggerMatcher.ConnectionStatusMatches(trigger, actual));
    }
}
