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
    private readonly DeviceConfigMessageBuilder configMessageBuilder = new();

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
        await connection.SendLineAsync(
            configMessageBuilder.Build(state, now, LastKnownSystemVolumePercent),
            cancellationToken);
    }

    public async Task SendTemporaryTemplateAsync(
        IMakroPadConnection? connection,
        IReadOnlyList<string> lines,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        if (connection is null || connection.State != DeviceConnectionState.Connected || lines.Count == 0)
        {
            return;
        }

        var values = new Dictionary<string, string?>
        {
            ["mode"] = "template",
            ["durationMs"] = Math.Max(0, durationMs).ToString()
        };

        for (var index = 0; index < Math.Min(lines.Count, 5); index++)
        {
            values[$"line{index + 1}"] = lines[index];
        }

        await connection.SendLineAsync(ProtocolCodec.Format("display", values), cancellationToken);
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
        return configMessageBuilder.UsesMediaPlaceholder(state);
    }
}
