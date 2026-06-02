using System;
using System.Linq;

namespace MakroPad.App.Services;

public sealed class OledDefaultScreenRenderer
{
    private const int MaxPreviewCharacters = 21;

    public OledDefaultScreenPreview RenderPreview(
        string title,
        string line1,
        string line2,
        string line3,
        string line4,
        OledDefaultScreenRenderContext context)
    {
        return new OledDefaultScreenPreview(
            FitPreviewLine(RenderTemplate(title, context)),
            FitPreviewLine(RenderTemplate(line1, context)),
            FitPreviewLine(RenderTemplate(line2, context)),
            FitPreviewLine(RenderTemplate(line3, context)),
            FitPreviewLine(RenderTemplate(line4, context)));
    }

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

    public static bool UsesMediaPlaceholder(params string[] templates)
    {
        return templates.Any(template => template.Contains("{media.", StringComparison.OrdinalIgnoreCase));
    }

    public static int NormalizeRows(int rows)
    {
        return rows == 5 ? 5 : 3;
    }

    public static string NormalizeLine(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string FitPreviewLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= MaxPreviewCharacters
            ? trimmed
            : trimmed[..(MaxPreviewCharacters - 2)] + "..";
    }
}
