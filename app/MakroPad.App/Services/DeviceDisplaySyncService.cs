using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MakroPad.Actions;
using MakroPad.Core.Protocol;
using MakroPad.Device;

namespace MakroPad.App.Services;

public sealed class DeviceDisplaySyncService
{
    private readonly ISystemVolumeController systemVolumeController;
    private readonly OledDefaultScreenRenderer oledDefaultScreenRenderer = new();

    public int? LastKnownSystemVolumePercent { get; private set; }

    public DeviceDisplaySyncService(ISystemVolumeController systemVolumeController)
    {
        this.systemVolumeController = systemVolumeController;
    }

    public async Task SendConfigAsync(
        IMakroPadConnection? connection,
        DeviceDisplayState state,
        CancellationToken cancellationToken = default)
    {
        if (connection is null || connection.State != DeviceConnectionState.Connected)
        {
            return;
        }

        var now = DateTime.Now;
        LastKnownSystemVolumePercent = await systemVolumeController.GetMasterVolumePercentAsync(cancellationToken);
        var line = ProtocolCodec.Format(
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
                ["time"] = now.ToString("HH:mm"),
                ["date"] = now.ToString("dd.MM.yyyy"),
                ["volumeSystem"] = LastKnownSystemVolumePercent is null ? null : $"{LastKnownSystemVolumePercent}%",
                ["preferredDisplay"] = state.EnableNowPlayingDisplay
                    && state.NowPlayingTrack is not null
                    && !UsesMediaPlaceholder(state)
                        ? "media"
                        : "profile"
            });

        await connection.SendLineAsync(line, cancellationToken);
    }

    public OledDefaultScreenPreview RenderPreview(DeviceDisplayState state)
    {
        return oledDefaultScreenRenderer.RenderPreview(
            state.DefaultScreenTitle,
            state.DefaultScreenLine1,
            state.DefaultScreenLine2,
            state.DefaultScreenLine3,
            state.DefaultScreenLine4,
            new OledDefaultScreenRenderContext(
                state.ActiveProfileName,
                state.ConnectionStatus,
                state.NowPlayingTrack,
                LastKnownSystemVolumePercent,
                DateTime.Now));
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
