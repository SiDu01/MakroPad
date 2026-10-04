using MakroPad.Core.Configuration;

namespace MakroPad.Core.DisplayTriggers;

public static class DisplayTriggerNormalizer
{
    public const string DefaultTemplate = "Slider {slider.id}\n{slider.percent}%";

    public static DisplayTriggerConfig Normalize(DisplayTriggerConfig trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var minPercent = NormalizePercent(trigger.MinPercent);

        return trigger with
        {
            EventType = NormalizeEventType(trigger.EventType),
            Input = string.IsNullOrWhiteSpace(trigger.Input) ? "slider:1" : trigger.Input.Trim(),
            ActionStatus = NormalizeActionStatus(trigger.ActionStatus),
            ConnectionStatus = NormalizeConnectionStatus(trigger.ConnectionStatus),
            Condition = NormalizeCondition(trigger.Condition),
            MinPercent = minPercent,
            MaxPercent = Math.Max(minPercent, NormalizePercent(trigger.MaxPercent)),
            ChangePercent = NormalizeChangePercent(trigger.ChangePercent),
            DurationMs = NormalizeDurationMs(trigger.DurationMs),
            TemplateMode = NormalizeTemplateMode(trigger.TemplateMode),
            Template = string.IsNullOrWhiteSpace(trigger.Template) ? DefaultTemplate : trigger.Template.Trim()
        };
    }

    public static string NormalizeEventType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "actionresult" => "actionResult",
            "mediachanged" => "mediaChanged",
            "connectionchanged" => "connectionChanged",
            "profilechanged" => "profileChanged",
            _ => "input"
        };
    }

    public static string NormalizeActionStatus(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "ok" or "failed" => value.ToLowerInvariant(),
            _ => "any"
        };
    }

    public static string NormalizeConnectionStatus(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "connected" or "connecting" or "unresponsive" or "disconnected" => value.ToLowerInvariant(),
            _ => "any"
        };
    }

    public static string NormalizeCondition(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "inrange" => "inRange",
            "increasing" or "decreasing" => value.ToLowerInvariant(),
            _ => "any"
        };
    }

    public static string NormalizeTemplateMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "html" => "html",
            "markdown" => "markdown",
            "layout" => "layout",
            _ => "normal"
        };
    }

    public static int NormalizePercent(int value) => Math.Clamp(value, 0, 100);

    public static int NormalizeChangePercent(int value) => Math.Clamp(value, 0, 100);

    public static int NormalizeDurationMs(int value) => Math.Clamp(value, 0, 60000);
}
