using System;
using System.Collections.Generic;

using MakroPad.Core.Protocol;

namespace MakroPad.App.Services;

public sealed class DeviceConfigMessageBuilder
{
    public string Build(DeviceDisplayState state, DateTime timestamp, int? systemVolumePercent)
    {
        return ProtocolCodec.Format(
            "config",
            new Dictionary<string, string?>
            {
                ["profile"] = state.ActiveProfileName,
                ["actionResultTimeoutMs"] = Math.Max(0, state.ActionResultTimeoutMs).ToString(),
                ["defaultRows"] = OledDefaultScreenRenderer.NormalizeRows(state.DefaultScreenRows).ToString(),
                ["defaultTitle"] = OledDefaultScreenRenderer.NormalizeLine(state.DefaultScreenTitle, "MakroPad"),
                ["defaultLine1"] = OledDefaultScreenRenderer.NormalizeLine(state.DefaultScreenLine1, "{profile}"),
                ["defaultLine2"] = OledDefaultScreenRenderer.NormalizeLine(state.DefaultScreenLine2, "{status}"),
                ["defaultLine3"] = state.DefaultScreenLine3.Trim(),
                ["defaultLine4"] = state.DefaultScreenLine4.Trim(),
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
}
