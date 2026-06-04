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
        var defaultLines = OledDefaultScreenRenderer.RenderDeviceLines(state)
            .Concat(Enumerable.Repeat(new DisplayTemplateLine("", 1), 5))
            .Take(5)
            .ToList();

        return ProtocolCodec.Format(
            "config",
            new Dictionary<string, string?>
            {
                ["profile"] = state.ActiveProfileName,
                ["actionResultTimeoutMs"] = Math.Max(0, state.ActionResultTimeoutMs).ToString(),
                ["defaultRows"] = OledDefaultScreenRenderer.NormalizeRows(state.DefaultScreenRows).ToString(),
                ["defaultTemplateMode"] = OledDefaultScreenRenderer.NormalizeTemplateMode(state.DefaultScreenTemplateMode),
                ["defaultTitle"] = defaultLines[0].Text,
                ["defaultTitleSize"] = FormatTextSize(defaultLines[0].NormalizedTextSize),
                ["defaultLine1"] = defaultLines[1].Text,
                ["defaultLine1Size"] = FormatTextSize(defaultLines[1].NormalizedTextSize),
                ["defaultLine2"] = defaultLines[2].Text,
                ["defaultLine2Size"] = FormatTextSize(defaultLines[2].NormalizedTextSize),
                ["defaultLine3"] = defaultLines[3].Text,
                ["defaultLine3Size"] = FormatTextSize(defaultLines[3].NormalizedTextSize),
                ["defaultLine4"] = defaultLines[4].Text,
                ["defaultLine4Size"] = FormatTextSize(defaultLines[4].NormalizedTextSize),
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
        return OledDefaultScreenRenderer.UsesMediaPlaceholder(
            state.DefaultScreenTitle,
            state.DefaultScreenLine1,
            state.DefaultScreenLine2,
            state.DefaultScreenLine3,
            state.DefaultScreenLine4);
    }

    private static string? FormatTextSize(int textSize)
    {
        var normalized = OledDefaultScreenRenderer.NormalizeTextSize(textSize);
        return normalized > 1 ? normalized.ToString() : null;
    }
}
