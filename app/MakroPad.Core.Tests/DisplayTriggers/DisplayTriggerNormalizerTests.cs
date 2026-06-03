using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Tests.DisplayTriggers;

public sealed class DisplayTriggerNormalizerTests
{
    [Fact]
    public void NormalizeAppliesDefaultsTrimmingAndBounds()
    {
        var trigger = new DisplayTriggerConfig
        {
            Enabled = true,
            EventType = "unknown",
            Input = "  ",
            ActionStatus = "UNKNOWN",
            ConnectionStatus = "CONNECTED",
            Condition = "INRANGE",
            MinPercent = 120,
            MaxPercent = -10,
            ChangePercent = 101,
            DurationMs = -1,
            TemplateMode = "HTML",
            Template = "  "
        };

        var result = DisplayTriggerNormalizer.Normalize(trigger);

        Assert.True(result.Enabled);
        Assert.Equal("input", result.EventType);
        Assert.Equal("slider:1", result.Input);
        Assert.Equal("any", result.ActionStatus);
        Assert.Equal("connected", result.ConnectionStatus);
        Assert.Equal("inRange", result.Condition);
        Assert.Equal(100, result.MinPercent);
        Assert.Equal(100, result.MaxPercent);
        Assert.Equal(100, result.ChangePercent);
        Assert.Equal(0, result.DurationMs);
        Assert.Equal("html", result.TemplateMode);
        Assert.Equal(DisplayTriggerNormalizer.DefaultTemplate, result.Template);
    }

    [Fact]
    public void NormalizePreservesSupportedValuesAndTrimsText()
    {
        var trigger = new DisplayTriggerConfig
        {
            EventType = "actionResult",
            Input = " button:2 ",
            ActionStatus = "failed",
            ConnectionStatus = "unresponsive",
            Condition = "inRange",
            MinPercent = 25,
            MaxPercent = 75,
            ChangePercent = 5,
            DurationMs = 1500,
            TemplateMode = "normal",
            Template = "  Action\n{action.message}  "
        };

        var result = DisplayTriggerNormalizer.Normalize(trigger);

        Assert.Equal("actionResult", result.EventType);
        Assert.Equal("button:2", result.Input);
        Assert.Equal("failed", result.ActionStatus);
        Assert.Equal("unresponsive", result.ConnectionStatus);
        Assert.Equal("inRange", result.Condition);
        Assert.Equal(25, result.MinPercent);
        Assert.Equal(75, result.MaxPercent);
        Assert.Equal(5, result.ChangePercent);
        Assert.Equal(1500, result.DurationMs);
        Assert.Equal("normal", result.TemplateMode);
        Assert.Equal("Action\n{action.message}", result.Template);
    }

    [Theory]
    [InlineData("input", "input")]
    [InlineData("actionResult", "actionResult")]
    [InlineData("mediaChanged", "mediaChanged")]
    [InlineData("connectionChanged", "connectionChanged")]
    [InlineData("profileChanged", "profileChanged")]
    [InlineData("ActionResult", "actionResult")]
    [InlineData(null, "input")]
    public void NormalizeEventTypeUsesSupportedExactValues(string? value, string expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeEventType(value));
    }

    [Theory]
    [InlineData("ok", "ok")]
    [InlineData("failed", "failed")]
    [InlineData("OK", "ok")]
    [InlineData(null, "any")]
    public void NormalizeActionStatusUsesSupportedExactValues(string? value, string expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeActionStatus(value));
    }

    [Theory]
    [InlineData("connected", "connected")]
    [InlineData("CONNECTING", "connecting")]
    [InlineData("Unresponsive", "unresponsive")]
    [InlineData("disconnected", "disconnected")]
    [InlineData("unknown", "any")]
    [InlineData(null, "any")]
    public void NormalizeConnectionStatusIsCaseInsensitive(string? value, string expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeConnectionStatus(value));
    }

    [Theory]
    [InlineData("any", "any")]
    [InlineData("inRange", "inRange")]
    [InlineData("increasing", "increasing")]
    [InlineData("decreasing", "decreasing")]
    [InlineData("InRange", "inRange")]
    [InlineData(null, "any")]
    public void NormalizeConditionUsesSupportedExactValues(string? value, string expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeCondition(value));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(101, 100)]
    public void NormalizePercentClampsToDisplayRange(int value, int expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizePercent(value));
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeChangePercent(value));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(3000, 3000)]
    [InlineData(60001, 60000)]
    public void NormalizeDurationClampsToSupportedRange(int value, int expected)
    {
        Assert.Equal(expected, DisplayTriggerNormalizer.NormalizeDurationMs(value));
    }
}
