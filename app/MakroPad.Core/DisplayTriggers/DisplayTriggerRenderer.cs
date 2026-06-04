using System.Net;
using System.Text.RegularExpressions;
using MakroPad.Core.Configuration;

namespace MakroPad.Core.DisplayTriggers;

public static partial class DisplayTriggerRenderer
{
    public const int MaxPreviewCharacters = 21;

    public static IReadOnlyList<string> RenderLines(
        DisplayTriggerConfig trigger,
        DisplayTriggerRenderContext context) =>
        RenderTemplateLines(trigger, context)
            .Select(line => line.Text)
            .ToList();

    public static IReadOnlyList<DisplayTemplateLine> RenderTemplateLines(
        DisplayTriggerConfig trigger,
        DisplayTriggerRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(context);

        var text = ReplacePlaceholders(trigger.Template, context);
        if (DisplayTriggerNormalizer.NormalizeTemplateMode(trigger.TemplateMode) == "html")
        {
            return RenderHtmlLikeLines(text);
        }

        return SplitDisplayTextLines(text)
            .Select(line => new DisplayTemplateLine(line))
            .ToList();
    }

    private static IReadOnlyList<string> SplitDisplayTextLines(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .Take(5)
            .ToList();
    }

    public static string ReplacePlaceholders(string template, DisplayTriggerRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        var sliderId = TryParseInputId(context.Input, out var id) ? id.ToString() : "";
        var mediaTrack = string.IsNullOrWhiteSpace(context.MediaArtist)
            ? context.MediaTitle
            : $"{context.MediaArtist} - {context.MediaTitle}";

        return template
            .Replace("{input}", context.Input, StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.id}", sliderId, StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.value}", context.SliderValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.percent}", context.SliderPercent?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{action.status}", context.ActionStatus, StringComparison.OrdinalIgnoreCase)
            .Replace("{action.name}", context.ActionName, StringComparison.OrdinalIgnoreCase)
            .Replace("{action.message}", context.ActionMessage, StringComparison.OrdinalIgnoreCase)
            .Replace("{media.artist}", context.MediaArtist, StringComparison.OrdinalIgnoreCase)
            .Replace("{media.title}", context.MediaTitle, StringComparison.OrdinalIgnoreCase)
            .Replace("{media.track}", mediaTrack, StringComparison.OrdinalIgnoreCase)
            .Replace("{connection.status}", context.ConnectionStatus, StringComparison.OrdinalIgnoreCase)
            .Replace("{profile}", context.Profile, StringComparison.OrdinalIgnoreCase)
            .Replace("{status}", context.Status, StringComparison.OrdinalIgnoreCase);
    }

    public static string RenderHtmlLikeText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return string.Join(
            "\n",
            RenderHtmlLikeLines(html).Select(line => line.Text));
    }

    public static IReadOnlyList<DisplayTemplateLine> RenderHtmlLikeLines(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var text = HeadingTagRegex().Replace(
            html,
            match => "\n" + HeadingMarker + StripHtml(match.Groups[1].Value) + "\n");
        text = BreakTagRegex().Replace(text, "\n");
        text = BlockClosingTagRegex().Replace(text, "\n");
        text = ListItemOpeningTagRegex().Replace(text, "- ");
        text = StripHtml(text);

        return SplitDisplayTextLines(text)
            .Select(line =>
            {
                if (line.StartsWith(HeadingMarker, StringComparison.Ordinal))
                {
                    return new DisplayTemplateLine(line[HeadingMarker.Length..].Trim(), 2);
                }

                return new DisplayTemplateLine(line);
            })
            .Where(line => line.Text.Length > 0)
            .Take(5)
            .ToList();
    }

    public static int MapSliderValueToPercent(int sliderValue)
    {
        return Math.Clamp((int)Math.Round(Math.Clamp(sliderValue, 0, 4095) / 4095.0 * 100), 0, 100);
    }

    public static IReadOnlyList<string> FitPreviewLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return FitPreviewLines(lines.Select(line => new DisplayTemplateLine(line)));
    }

    public static IReadOnlyList<string> FitPreviewLines(IEnumerable<DisplayTemplateLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines
            .Select(line => FitPreviewLine(line.Text, line.NormalizedTextSize))
            .ToList();
    }

    public static bool TryParseInputId(string input, out int id)
    {
        ArgumentNullException.ThrowIfNull(input);

        id = 0;
        var separatorIndex = input.IndexOf(':');
        return separatorIndex >= 0
            && int.TryParse(input[(separatorIndex + 1)..], out id)
            && id > 0;
    }

    private const string HeadingMarker = "\u0002";

    private static string FitPreviewLine(string line, int textSize)
    {
        var trimmed = line.Trim();
        var maxCharacters = Math.Max(1, MaxPreviewCharacters / Math.Clamp(textSize, 1, 2));

        return trimmed.Length <= maxCharacters
            ? trimmed
            : $"{trimmed[..Math.Max(1, maxCharacters - 2)]}..";
    }

    private static string StripHtml(string html)
    {
        var text = AnyTagRegex().Replace(html, "");
        return WebUtility.HtmlDecode(text);
    }

    [GeneratedRegex("(?i)<\\s*br\\s*/?\\s*>")]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex("(?is)<\\s*h[1-2][^>]*>(.*?)</\\s*h[1-2]\\s*>")]
    private static partial Regex HeadingTagRegex();

    [GeneratedRegex("(?i)</\\s*(p|div|h[1-6]|li|tr)\\s*>")]
    private static partial Regex BlockClosingTagRegex();

    [GeneratedRegex("(?i)<\\s*li\\s*>")]
    private static partial Regex ListItemOpeningTagRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTagRegex();
}
