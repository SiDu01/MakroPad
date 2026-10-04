using System;
using System.Collections.Generic;
using System.Linq;

using MakroPad.Core.DisplayTriggers;
using MakroPad.Core.Protocol;

namespace MakroPad.App.Services;

public sealed class DeviceConfigMessageBuilder
{
    public string Build(DeviceDisplayState state, DateTime timestamp, int? systemVolumePercent)
    {
        var lines = state.DefaultLines
            .Concat(Enumerable.Repeat(new DisplayTemplateLine(""), 5))
            .Take(5)
            .ToList();

        return ProtocolCodec.Format(
            "config",
            new Dictionary<string, string?>
            {
                ["profile"] = state.ActiveProfileName,
                ["actionResultTimeoutMs"] = Math.Max(0, state.ActionResultTimeoutMs).ToString(),
                ["defaultRows"] = OledDefaultScreenRenderer.NormalizeRows(state.DefaultScreenRows).ToString(),
                ["defaultTemplateMode"] = "normal",
                ["defaultTitle"] = lines[0].Text,
                ["defaultTitleSize"] = FormatTextSize(lines[0].NormalizedTextSize),
                ["defaultTitleAlign"] = FormatAlign(lines[0].NormalizedAlignment),
                ["defaultLine1"] = lines[1].Text,
                ["defaultLine1Size"] = FormatTextSize(lines[1].NormalizedTextSize),
                ["defaultLine1Align"] = FormatAlign(lines[1].NormalizedAlignment),
                ["defaultLine2"] = lines[2].Text,
                ["defaultLine2Size"] = FormatTextSize(lines[2].NormalizedTextSize),
                ["defaultLine2Align"] = FormatAlign(lines[2].NormalizedAlignment),
                ["defaultLine3"] = lines[3].Text,
                ["defaultLine3Size"] = FormatTextSize(lines[3].NormalizedTextSize),
                ["defaultLine3Align"] = FormatAlign(lines[3].NormalizedAlignment),
                ["defaultLine4"] = lines[4].Text,
                ["defaultLine4Size"] = FormatTextSize(lines[4].NormalizedTextSize),
                ["defaultLine4Align"] = FormatAlign(lines[4].NormalizedAlignment),
                ["time"] = timestamp.ToString("HH:mm"),
                ["date"] = timestamp.ToString("dd.MM.yyyy"),
                ["volumeSystem"] = systemVolumePercent is null ? null : $"{systemVolumePercent}%",
                ["preferredDisplay"] = state.EnableNowPlayingDisplay
                    && state.NowPlayingTrack is not null
                    && !UsesMediaPlaceholder(state)
                        ? "media"
                        : "profile"
            });
    }

    public bool UsesMediaPlaceholder(DeviceDisplayState state)
    {
        return state.DefaultLines.Any(line => line.Text.Contains("{media.", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FormatTextSize(int textSize)
    {
        var normalized = OledDefaultScreenRenderer.NormalizeTextSize(textSize);
        return normalized > 1 ? normalized.ToString() : null;
    }

    private static string? FormatAlign(string alignment) => alignment == "left" ? null : alignment;
}
