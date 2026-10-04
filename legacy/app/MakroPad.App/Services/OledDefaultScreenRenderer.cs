using System;

namespace MakroPad.App.Services;

/// <summary>
/// Resolves the dynamic placeholders of an idle/default screen line for the app preview
/// (the firmware resolves them live on the device). Also provides shared normalization helpers.
/// </summary>
public sealed class OledDefaultScreenRenderer
{
    public string RenderTemplate(string template, OledDefaultScreenRenderContext context)
    {
        var artist = context.NowPlayingTrack?.Artist ?? "";
        var title = context.NowPlayingTrack?.Title ?? "";
        var mediaTrack = string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)
            ? title
            : $"{artist} - {title}";

        return NormalizeLine(template, "")
            .Replace("{profile}", context.ActiveProfileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{status}", context.ConnectionStatus, StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", context.Timestamp.ToString("HH:mm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", context.Timestamp.ToString("dd.MM.yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{media.artist}", artist, StringComparison.OrdinalIgnoreCase)
            .Replace("{media.title}", title, StringComparison.OrdinalIgnoreCase)
            .Replace("{media.track}", mediaTrack, StringComparison.OrdinalIgnoreCase)
            .Replace("{volume.system}", context.SystemVolumePercent is null ? "" : $"{context.SystemVolumePercent}%", StringComparison.OrdinalIgnoreCase)
            .Replace("{device.uptime}", "00:00:00", StringComparison.OrdinalIgnoreCase);
    }

    public static int NormalizeRows(int rows)
    {
        return rows == 5 ? 5 : 3;
    }

    public static int NormalizeTextSize(int textSize)
    {
        return Math.Clamp(textSize, 1, 2);
    }

    public static string NormalizeLine(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
