using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MakroPad.Actions;
using MakroPad.Core.DisplayTriggers;
using MakroPad.Core.Protocol;
using MakroPad.Device;

namespace MakroPad.App.Services;

public sealed class DeviceDisplaySyncService
{
    private readonly ISystemVolumeController systemVolumeController;
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
        await SendTemporaryTemplateAsync(
            connection,
            lines.Select(line => new DisplayTemplateLine(line)).ToList(),
            durationMs,
            cancellationToken);
    }

    public async Task SendTemporaryTemplateAsync(
        IMakroPadConnection? connection,
        IReadOnlyList<DisplayTemplateLine> lines,
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
            var line = lines[index];
            values[$"line{index + 1}"] = line.Text;
            if (line.NormalizedTextSize > 1)
            {
                values[$"line{index + 1}Size"] = line.NormalizedTextSize.ToString();
            }

            if (line.NormalizedAlignment != "left")
            {
                values[$"line{index + 1}Align"] = line.NormalizedAlignment;
            }
        }

        await connection.SendLineAsync(ProtocolCodec.Format("display", values), cancellationToken);
    }

    public async Task SendTemporaryLayoutAsync(
        IMakroPadConnection? connection,
        DisplayLayoutScreen screen,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        if (connection is null || connection.State != DeviceConnectionState.Connected)
        {
            return;
        }

        await connection.SendLineAsync(
            ProtocolCodec.Format(
                "display",
                new Dictionary<string, string?>
                {
                    ["mode"] = "layout",
                    ["layout"] = screen.NormalizedLayout,
                    ["durationMs"] = Math.Max(0, durationMs).ToString(),
                    ["title"] = screen.Title,
                    ["line1"] = screen.Line1,
                    ["line2"] = screen.Line2,
                    ["value"] = screen.NormalizedValuePercent?.ToString(),
                    ["icon"] = string.IsNullOrWhiteSpace(screen.Icon) ? null : screen.Icon
                }),
            cancellationToken);
    }
}
