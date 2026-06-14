using System;
using System.Collections.Generic;
using System.Linq;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Services;

public sealed class OledDefaultScreenRenderer
{
    private const int MaxPreviewCharacters = 21;

    public static IReadOnlyList<DisplayTemplateLine> RenderDeviceLines(DeviceDisplayState state)
    {
        var sourceLines = new[]
        {
            new DisplayTemplateLine(state.DefaultScreenTitle.Trim(), state.DefaultScreenTitleSize),
            new DisplayTemplateLine(state.DefaultScreenLine1.Trim(), state.DefaultScreenLine1Size),
            new DisplayTemplateLine(state.DefaultScreenLine2.Trim(), state.DefaultScreenLine2Size),
            new DisplayTemplateLine(state.DefaultScreenLine3.Trim(), state.DefaultScreenLine3Size),
            new DisplayTemplateLine(state.DefaultScreenLine4.Trim(), state.DefaultScreenLine4Size)
        };
        var rowCount = NormalizeRows(state.DefaultScreenRows);

        if (NormalizeTemplateMode(state.DefaultScreenTemplateMode) != "html")
        {
            return sourceLines.Take(rowCount).ToList();
        }

        return sourceLines
            .Take(rowCount)
            .SelectMany(line => RenderHtmlField(line))
            .Take(rowCount)
            .ToList();
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

    public static string NormalizeTemplateMode(string? templateMode)
    {
        return string.Equals(templateMode, "html", StringComparison.OrdinalIgnoreCase)
            ? "html"
            : "normal";
    }

    public static int NormalizeTextSize(int textSize)
    {
        return Math.Clamp(textSize, 1, 2);
    }

    public static string NormalizeLine(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static IEnumerable<DisplayTemplateLine> RenderHtmlField(DisplayTemplateLine source)
    {
        return DisplayTriggerRenderer.RenderHtmlLines(source.Text)
            .Select(line => new DisplayTemplateLine(
                line.Text,
                line.NormalizedTextSize > 1
                    ? line.NormalizedTextSize
                    : source.NormalizedTextSize));
    }
}
