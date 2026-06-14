using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Services;

/// <summary>
/// Builds the device config state for a given active idle screen. The screen's content is
/// rendered app-side to OLED lines (placeholders kept raw so the firmware resolves them live),
/// so the config is sent as plain lines (mode "normal").
/// </summary>
public static class IdleScreenDeviceState
{
    public static DeviceDisplayState Build(
        IdleScreenConfig? screen,
        string activeProfileName,
        string connectionStatus,
        int actionResultTimeoutMs,
        bool enableNowPlayingDisplay,
        NowPlayingTrack? nowPlayingTrack)
    {
        var template = string.IsNullOrWhiteSpace(screen?.Template) ? "{profile}\n{status}" : screen!.Template;
        var mode = screen?.TemplateMode ?? "markdown";
        var lines = DisplayTriggerRenderer.RenderContentLines(template, mode);

        DisplayTemplateLine Line(int index) =>
            index < lines.Count ? lines[index] : new DisplayTemplateLine("", 1);

        var rows = lines.Count > 3 ? 5 : 3;

        return new DeviceDisplayState(
            activeProfileName,
            connectionStatus,
            actionResultTimeoutMs,
            rows,
            "normal",
            Line(0).Text, Line(0).NormalizedTextSize,
            Line(1).Text, Line(1).NormalizedTextSize,
            Line(2).Text, Line(2).NormalizedTextSize,
            Line(3).Text, Line(3).NormalizedTextSize,
            Line(4).Text, Line(4).NormalizedTextSize,
            enableNowPlayingDisplay,
            nowPlayingTrack);
    }
}
