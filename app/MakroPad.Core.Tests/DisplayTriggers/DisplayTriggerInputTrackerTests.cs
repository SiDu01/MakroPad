using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Tests.DisplayTriggers;

public sealed class DisplayTriggerInputTrackerTests
{
    [Fact]
    public void DifferentThresholdsAccumulateIndependently()
    {
        var tracker = new DisplayTriggerInputTracker();
        var small = new DisplayTriggerConfig { ChangePercent = 2 };
        var large = new DisplayTriggerConfig { ChangePercent = 10 };

        Assert.False(tracker.ShouldTrigger("slider:1|small", small, 50));
        Assert.False(tracker.ShouldTrigger("slider:1|large", large, 50));

        Assert.True(tracker.ShouldTrigger("slider:1|small", small, 52));
        Assert.False(tracker.ShouldTrigger("slider:1|large", large, 52));
        Assert.True(tracker.ShouldTrigger("slider:1|small", small, 54));
        Assert.False(tracker.ShouldTrigger("slider:1|large", large, 54));
        Assert.True(tracker.ShouldTrigger("slider:1|large", large, 60));
    }

    [Fact]
    public void ZeroThresholdCanTriggerOnFirstMatchingSample()
    {
        var tracker = new DisplayTriggerInputTracker();
        var trigger = new DisplayTriggerConfig
        {
            ChangePercent = 0,
            Condition = "inRange",
            MinPercent = 40,
            MaxPercent = 60
        };

        Assert.True(tracker.ShouldTrigger("slider:1|range", trigger, 50));
        Assert.False(tracker.ShouldTrigger("slider:1|range", trigger, 50));
    }

    [Fact]
    public void ConditionUsesLastTriggeredPercent()
    {
        var tracker = new DisplayTriggerInputTracker();
        var trigger = new DisplayTriggerConfig
        {
            ChangePercent = 5,
            Condition = "increasing"
        };

        Assert.False(tracker.ShouldTrigger("slider:1|up", trigger, 50));
        Assert.False(tracker.ShouldTrigger("slider:1|up", trigger, 47));
        Assert.True(tracker.ShouldTrigger("slider:1|up", trigger, 55));
    }

    [Fact]
    public void ClearRemovesPreviousSamples()
    {
        var tracker = new DisplayTriggerInputTracker();
        var trigger = new DisplayTriggerConfig { ChangePercent = 5 };

        Assert.False(tracker.ShouldTrigger("slider:1|value", trigger, 50));
        tracker.Clear();

        Assert.False(tracker.ShouldTrigger("slider:1|value", trigger, 55));
    }
}
